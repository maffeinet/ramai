using System.Globalization;
using System.Security.Claims;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;
using Ramai.Application.Tenancy;

namespace Ramai.UnitTests;

public sealed class WorkspaceOperationsTests
{
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _tenant = Guid.NewGuid();

    private ClaimsPrincipal Session(bool mfa = true, bool selected = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _user.ToString()), new(AccountSessionClaims.SecurityStamp, "synthetic-stamp"),
            new(AccountSessionClaims.Started, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            // This must never grant a permission absent from membership.
            new(ClaimTypes.Role, "Owner")
        };
        if (selected) { claims.Add(new(TenantSessionClaims.SelectedTenant, _tenant.ToString())); }
        if (mfa) { claims.Add(new(AccountSessionClaims.Mfa, "true")); }
        return new(new ClaimsIdentity(claims, AccountSessionClaims.Scheme));
    }

    private WorkspaceOperationsService Service(Reader reader, Store store, bool sessionValid = true) =>
        new(new TenantContextResolver(new Sessions(sessionValid), reader, TimeProvider.System), store);
    private Reader Membership(TenantAccessRole role) => new() { Membership = new(_tenant, _user, role) };

    [Theory]
    [InlineData(TenantAccessRole.Owner, true, WorkspaceOperationOutcome.Accepted)]
    [InlineData(TenantAccessRole.Admin, true, WorkspaceOperationOutcome.Accepted)]
    [InlineData(TenantAccessRole.Member, true, WorkspaceOperationOutcome.Forbidden)]
    [InlineData(TenantAccessRole.Member, false, WorkspaceOperationOutcome.Forbidden)]
    [InlineData(TenantAccessRole.Owner, false, WorkspaceOperationOutcome.MfaRequired)]
    [InlineData(TenantAccessRole.Admin, false, WorkspaceOperationOutcome.MfaRequired)]
    public async Task Settings_role_matrix_uses_membership_and_mfa_not_role_claim(TenantAccessRole role, bool mfa, WorkspaceOperationOutcome expected)
    {
        var store = new Store();
        var service = Service(Membership(role), store);
        Assert.Equal(expected, (await service.ReadSettingsAsync(Session(mfa))).Outcome);
        Assert.Equal(expected, (await service.RenameAsync(Session(mfa), " New synthetic name ")).Outcome);
        Assert.Equal(expected == WorkspaceOperationOutcome.Accepted ? 2 : 0, store.Calls);
        if (expected == WorkspaceOperationOutcome.Accepted) { Assert.Equal("New synthetic name", store.Name); }
    }

    [Theory]
    [InlineData(TenantAccessRole.Owner)]
    [InlineData(TenantAccessRole.Admin)]
    [InlineData(TenantAccessRole.Member)]
    public async Task All_members_can_read_current_workspace(TenantAccessRole role)
    {
        var result = await Service(Membership(role), new()).ReadAsync(Session(mfa: role != TenantAccessRole.Member));
        Assert.Equal(WorkspaceOperationOutcome.Accepted, result.Outcome);
        Assert.Equal(_tenant, result.Workspace!.TenantId);
        Assert.Equal(role, result.Workspace.Role);
    }

    [Theory]
    [InlineData("anonymous", WorkspaceOperationOutcome.Unauthenticated)]
    [InlineData("revoked", WorkspaceOperationOutcome.Unauthenticated)]
    [InlineData("unselected", WorkspaceOperationOutcome.SelectionRequired)]
    [InlineData("absent", WorkspaceOperationOutcome.Forbidden)]
    public async Task Denial_does_not_call_data_port(string scenario, WorkspaceOperationOutcome expected)
    {
        var store = new Store();
        var reader = Membership(TenantAccessRole.Owner);
        if (scenario == "absent") { reader.Membership = null; }
        var service = Service(reader, store, scenario != "revoked");
        var principal = scenario == "anonymous" ? new ClaimsPrincipal() : Session(selected: scenario != "unselected");
        Assert.Equal(expected, (await service.ReadAsync(principal)).Outcome);
        Assert.Equal(expected, (await service.RenameAsync(principal, "Synthetic")).Outcome);
        Assert.Equal(0, store.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("synthetic\nname")]
    [InlineData("synthetic\uFFFDname")]
    public async Task Invalid_name_never_reaches_store(string? name)
    {
        var store = new Store();
        Assert.Equal(WorkspaceOperationOutcome.InvalidInput,
            (await Service(Membership(TenantAccessRole.Owner), store).RenameAsync(Session(), name!)).Outcome);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task Name_length_is_bounded_without_silent_truncation()
    {
        var store = new Store();
        var service = Service(Membership(TenantAccessRole.Admin), store);
        Assert.Equal(WorkspaceOperationOutcome.InvalidInput, (await service.RenameAsync(Session(), new string('x', 257))).Outcome);
        Assert.Equal(WorkspaceOperationOutcome.Accepted, (await service.RenameAsync(Session(), new string('x', 256))).Outcome);
        Assert.Equal(1, store.Calls);
    }

    [Fact]
    public async Task Each_operation_reloads_role_and_membership()
    {
        var reader = Membership(TenantAccessRole.Admin);
        var store = new Store();
        var service = Service(reader, store);
        var principal = Session();
        Assert.Equal(WorkspaceOperationOutcome.Accepted, (await service.RenameAsync(principal, "Synthetic")).Outcome);
        reader.Membership = new(_tenant, _user, TenantAccessRole.Member);
        Assert.Equal(WorkspaceOperationOutcome.Forbidden, (await service.RenameAsync(principal, "Denied")).Outcome);
        reader.Membership = null;
        Assert.Equal(WorkspaceOperationOutcome.Forbidden, (await service.ReadAsync(principal)).Outcome);
        Assert.Equal(1, store.Calls);
        Assert.Equal(3, reader.Calls);
    }

    [Fact]
    public async Task Missing_row_has_no_data_and_adapter_scope_mismatch_is_rejected()
    {
        var store = new Store { Missing = true };
        var service = Service(Membership(TenantAccessRole.Owner), store);
        var result = await service.ReadAsync(Session());
        Assert.Equal(WorkspaceOperationOutcome.NotFound, result.Outcome);
        Assert.Null(result.Workspace);
        store.Missing = false;
        store.Foreign = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(Session()));
    }

    [Fact]
    public async Task Dependency_failure_and_cancellation_have_no_success_fallback()
    {
        var store = new Store { Broken = true };
        var service = Service(Membership(TenantAccessRole.Owner), store);
        await Assert.ThrowsAsync<TimeoutException>(() => service.ReadAsync(Session()));
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RenameAsync(Session(), "Synthetic", source.Token));
        Assert.Equal(1, store.Calls);
    }

    private sealed class Reader : ITenantMembershipReader
    {
        public TenantMembershipInfo? Membership { get; set; }
        public int Calls { get; private set; }
        public Task<TenantMembershipInfo?> FindActiveAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(Membership); }
    }
    private sealed class Sessions(bool valid) : IAccountSessionValidator
    {
        public Task<bool> ValidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default) => Task.FromResult(valid);
        public Task<bool> RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Store : IWorkspaceDataStore
    {
        public int Calls { get; private set; }
        public string Name { get; private set; } = "Synthetic workspace";
        public bool Missing { get; set; }
        public bool Foreign { get; set; }
        public bool Broken { get; set; }
        public Task<WorkspaceInfo?> ReadAsync(TenantContext verifiedContext, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Broken) { throw new TimeoutException("Synthetic failure"); }
            return Task.FromResult<WorkspaceInfo?>(Missing ? null : new(Foreign ? Guid.NewGuid() : verifiedContext.TenantId, Name, verifiedContext.Role));
        }
        public Task<WorkspaceInfo?> RenameAsync(TenantContext verifiedContext, string name, CancellationToken cancellationToken = default)
        { Name = name; return ReadAsync(verifiedContext, cancellationToken); }
    }
}
