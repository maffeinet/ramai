using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ramai.Api.Health;
using Ramai.Api.Middleware;
using Ramai.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    options.UseUtcTimestamp = true;
});

builder.Services.AddRamaiInfrastructure(builder.Configuration);
builder.Services
    .AddHealthChecks()
    .AddCheck("application", () => HealthCheckResult.Healthy("Application is running."), ["self"])
    .AddCheck<PostgreSqlHealthCheck>("postgresql", tags: ["dependency", "database"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["dependency", "cache"]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapGet("/", () => Results.Ok(new { service = "RAMAI API", status = "foundation" }));
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.Run();

public partial class Program
{
}
