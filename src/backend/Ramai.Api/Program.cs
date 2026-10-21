using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ramai.Api.Health;
using Ramai.Api.Middleware;
using Ramai.Infrastructure;
using Ramai.Api.Authentication;
using Ramai.Api.Composition;
using Ramai.Api.Workspaces;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

builder.Services.AddRamaiInfrastructure(builder.Configuration);
var identityEnabled = builder.Services.AddConfiguredWebIdentity(builder.Configuration);
if (identityEnabled) { builder.Services.AddConfiguredWebTenants(builder.Configuration); }
builder.Services
    .AddHealthChecks()
    .AddCheck("application", () => HealthCheckResult.Healthy("Application is running."), ["self"])
    .AddCheck<PostgreSqlHealthCheck>("postgresql", tags: ["dependency", "database"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["dependency", "cache"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
if (identityEnabled)
{
    app.UseWorkspaceProblemDetails();
    app.UseMiddleware<AccountTransportMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapAccountEndpoints();
    app.MapWorkspaceEndpoints();
}

app.MapGet("/", () => Results.Ok(new { service = "RAMAI API", status = "foundation" }));
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.Run();

public partial class Program
{
}
