using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ramai.Api.Health;

public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            status = report.Status.ToString(),
            correlationId = context.TraceIdentifier,
            totalDurationMilliseconds = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMilliseconds = entry.Value.Duration.TotalMilliseconds,
                    data = entry.Value.Data
                })
        };

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            SerializerOptions,
            context.RequestAborted);
    }
}
