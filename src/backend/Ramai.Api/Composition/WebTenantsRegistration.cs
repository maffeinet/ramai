using Microsoft.EntityFrameworkCore;
using Ramai.Application.Contracts.Tenants;
using Ramai.Application.Tenancy;
using Ramai.Infrastructure.Configuration;
using Ramai.Modules.Tenants.Persistence;

namespace Ramai.Api.Composition;

// ADR-010: composition only, not an application handler or cross-module table access.
public static class WebTenantsRegistration
{
    public static void AddConfiguredWebTenants(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = new PostgreSqlOptions(configuration["RAMAI_DB_HOST"] ?? "localhost",
            configuration.GetValue("RAMAI_DB_PORT", 5432), configuration["RAMAI_DB_NAME"] ?? "ramai_dev",
            configuration["RAMAI_DB_USER"] ?? "ramai", configuration["RAMAI_DB_PASSWORD"]
                ?? throw new InvalidOperationException("Missing required database configuration.")).ToConnectionString();
        services.AddDbContextFactory<TenantsStoreContext>(options => options.UseNpgsql(connection,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
        services.AddSingleton(new DbContextOptionsBuilder<TenantDataContext>().UseNpgsql(connection).Options);
        services.AddScoped<ITenantMembershipReader, TenantMembershipReader>();
        services.AddScoped<ITenantContextResolver, TenantContextResolver>();
        services.AddScoped<IWorkspaceProvisioningStore, WorkspaceProvisioningStore>();
        services.AddScoped<IWorkspaceProvisioner, WorkspaceProvisioningService>();
        services.AddScoped<IWorkspaceDataStore, WorkspaceDataStore>();
        services.AddScoped<IWorkspaceOperations, WorkspaceOperationsService>();
        services.AddSingleton<Workspaces.IWorkspaceRequestBudget, WorkspaceRequestBudget>();
    }
}
