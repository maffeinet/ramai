using System.Diagnostics;

namespace Ramai.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaximumLength = 128;

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName].ToString());
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Environment"] = environment.EnvironmentName
        });

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP request completed with status code {StatusCode} in {ElapsedMilliseconds} ms",
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static string ResolveCorrelationId(string candidate) =>
        IsValid(candidate) ? candidate : Guid.NewGuid().ToString("N");

    private static bool IsValid(string candidate) =>
        candidate.Length is > 0 and <= MaximumLength &&
        candidate.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':');
}
