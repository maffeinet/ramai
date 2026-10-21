using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Ramai.Api.Composition;
using Ramai.Api.Workspaces;
using StackExchange.Redis;

namespace Ramai.Api.Middleware;

public sealed class AccountTransportMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, WebIdentityOrigin origin, ILogger<AccountTransportMiddleware> logger)
    {
        // Surround authentication too: a Redis/DB outage must not leak an exception or grant access.
        var accountRequest = context.Request.Path.StartsWithSegments("/api/v1/account") || WorkspaceEndpoints.IsWorkspaceRequest(context);
        if (accountRequest)
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (!context.Request.IsHttps || (context.Request.Headers.TryGetValue("Origin", out var supplied)
                && supplied.ToString() != origin.Value))
            {
                context.Response.StatusCode = 400;
                return;
            }
            var maximum = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (maximum is { IsReadOnly: false }) { maximum.MaxRequestBodySize = 4096; }
            if (context.Request.ContentLength > 4096) { context.Response.StatusCode = 413; return; }
        }
        try { await next(context); }
        catch (Exception exception) when (!context.Response.HasStarted && (exception is DbException or DbUpdateException
            or RedisException or TimeoutException or CryptographicException or ObjectDisposedException
            or InvalidOperationException { InnerException: DbException }))
        {
            logger.LogWarning("Account session dependency operation failed; access denied");
            context.Response.Clear();
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = context.TraceIdentifier;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.StatusCode = 503;
        }
    }
}
