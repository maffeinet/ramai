using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Ramai.Api.Authentication;
using Ramai.Application.Contracts.Identity;
using Ramai.Application.Contracts.Tenants;

namespace Ramai.Api.Workspaces;

public static class WorkspaceEndpoints
{
    public static bool IsWorkspaceRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api/v1/profile")
        || context.Request.Path.StartsWithSegments("/api/v1/workspace")
        || context.Request.Path.StartsWithSegments("/api/v1/workspaces");

    public static void UseWorkspaceProblemDetails(this IApplicationBuilder app) =>
        app.UseWhen(IsWorkspaceRequest, branch => branch.UseStatusCodePages(async status =>
        {
            var context = status.HttpContext;
            await Results.Problem(statusCode: context.Response.StatusCode,
                title: ReasonPhrases.GetReasonPhrase(context.Response.StatusCode),
                extensions: new Dictionary<string, object?> { ["correlationId"] = context.TraceIdentifier }).ExecuteAsync(context);
        }));

    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").RequireAuthorization();
        group.MapGet("/profile", async (HttpContext context, IUserAccountReader accounts) =>
        {
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) { return Results.StatusCode(401); }
            var account = await accounts.FindAsync(id, context.RequestAborted);
            return account is null || account.UserId != id || !account.CanAccessTenantWorkspaces
                ? Results.StatusCode(401) : Results.Ok(new
                {
                    userId = id,
                    emailConfirmed = true,
                    mfaVerified = context.User.FindFirstValue(AccountSessionClaims.Mfa) == "true"
                });
        });
        group.MapGet("/workspace", async (HttpContext context, IWorkspaceOperations service) =>
            Result(context, "ReadWorkspace", await service.ReadAsync(context.User, context.RequestAborted)));
        group.MapGet("/workspace/settings", async (HttpContext context, IWorkspaceOperations service) =>
            Result(context, "ReadSettings", await service.ReadSettingsAsync(context.User, context.RequestAborted)));
        group.MapPut("/workspace/settings", Rename);
        group.MapPost("/workspaces", Provision);
        group.MapPost("/workspaces/select", Select);
        // No arbitrary tenant route is an access grant. Unknown workspace paths yield generic 404.
    }

    private static async Task<IResult> Rename(HttpContext context, IAntiforgery antiforgery, IWorkspaceOperations service)
    {
        if (!await AccountEndpoints.ValidateCsrf(context, antiforgery)) { return Results.StatusCode(400); }
        var input = await WorkspaceHttpInput.Read<RenameRequest>(context);
        if (input.Value?.Name is null) { return Results.StatusCode(input.Error == 0 ? 400 : input.Error); }
        return Result(context, "RenameWorkspace", await service.RenameAsync(context.User, input.Value.Name, context.RequestAborted));
    }

    private static async Task<IResult> Provision(HttpContext context, IAntiforgery antiforgery,
        IWorkspaceProvisioner service, IWorkspaceRequestBudget budget)
    {
        if (!await AccountEndpoints.ValidateCsrf(context, antiforgery)) { return Results.StatusCode(400); }
        var input = await WorkspaceHttpInput.Read<ProvisionRequest>(context);
        if (input.Value?.Name is null || input.Value.RequestId == Guid.Empty)
        { return Results.StatusCode(input.Error == 0 ? 400 : input.Error); }
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) { return Results.StatusCode(401); }
        var origin = context.Connection.RemoteIpAddress;
        if (origin is null) { return Results.StatusCode(503); }
        if (!await budget.AllowProvisioningAsync(userId, origin, context.RequestAborted))
        { context.Response.Headers.RetryAfter = "60"; return Results.StatusCode(429); }
        var result = await service.CreateAsync(context.User, input.Value.Name, input.Value.RequestId,
            context.TraceIdentifier, context.RequestAborted);
        Log(context, "ProvisionWorkspace", result.Outcome.ToString());
        return result.Outcome switch
        {
            WorkspaceProvisioningOutcome.Created => Results.Json(new { workspaceId = result.TenantId }, statusCode: 201),
            WorkspaceProvisioningOutcome.Replayed => Results.Ok(new { workspaceId = result.TenantId }),
            WorkspaceProvisioningOutcome.Conflict => Results.StatusCode(409),
            WorkspaceProvisioningOutcome.Unauthenticated => Results.StatusCode(401),
            WorkspaceProvisioningOutcome.MfaRequired => Results.StatusCode(403),
            _ => Results.StatusCode(400)
        };
    }

    private static async Task<IResult> Select(HttpContext context, IAntiforgery antiforgery, ITenantContextResolver resolver)
    {
        if (!await AccountEndpoints.ValidateCsrf(context, antiforgery)) { return Results.StatusCode(400); }
        var input = await WorkspaceHttpInput.Read<SelectRequest>(context);
        if (input.Value is null || input.Value.WorkspaceId == Guid.Empty)
        { return Results.StatusCode(input.Error == 0 ? 400 : input.Error); }
        var decision = await resolver.SelectAsync(context.User, input.Value.WorkspaceId, context.RequestAborted);
        Log(context, "SelectWorkspace", decision.Outcome.ToString());
        if (decision.Outcome != TenantContextOutcome.Accepted || decision.SelectedPrincipal is null)
        { return Results.StatusCode(decision.Outcome == TenantContextOutcome.Unauthenticated ? 401 : 403); }
        var ticket = await context.AuthenticateAsync(AccountSessionClaims.Scheme);
        if (!ticket.Succeeded || ticket.Properties is null) { return Results.StatusCode(401); }
        // Preserve the absolute start marker and existing cookie properties; never invent a new login.
        await context.SignInAsync(AccountSessionClaims.Scheme, decision.SelectedPrincipal, ticket.Properties);
        return Results.NoContent();
    }

    private static IResult Result(HttpContext context, string operation, WorkspaceOperationResult result)
    {
        Log(context, operation, result.Outcome.ToString());
        return result.Outcome switch
        {
            WorkspaceOperationOutcome.Accepted when result.Workspace is not null => Results.Ok(new
            {
                workspaceId = result.Workspace.TenantId,
                name = result.Workspace.Name,
                role = result.Workspace.Role.ToString()
            }),
            WorkspaceOperationOutcome.Unauthenticated => Results.StatusCode(401),
            WorkspaceOperationOutcome.SelectionRequired => Results.StatusCode(409),
            WorkspaceOperationOutcome.NotFound => Results.StatusCode(404),
            WorkspaceOperationOutcome.InvalidInput => Results.StatusCode(400),
            _ => Results.StatusCode(403)
        };
    }
    private static void Log(HttpContext context, string operation, string outcome) =>
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Ramai.WorkspaceOperations")
            .LogInformation("Workspace operation {Operation} outcome {Outcome}", operation, outcome);

    private sealed class RenameRequest { public string? Name { get; init; } }
    private sealed class ProvisionRequest { public string? Name { get; init; } public Guid RequestId { get; init; } }
    private sealed class SelectRequest { public Guid WorkspaceId { get; init; } }
}
