using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Ramai.Application.Contracts.Identity;

namespace Ramai.Api.Authentication;

public static class MfaEndpoints
{
    public static void MapMfaEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/mfa/enrollment", (HttpContext context, IAntiforgery csrf, IAccountMfaService mfa) =>
            Manage(context, csrf, mfa, MfaOperation.BeginEnrollment)).RequireAuthorization();
        group.MapPost("/mfa/confirm", (HttpContext context, IAntiforgery csrf, IAccountMfaService mfa) =>
            Manage(context, csrf, mfa, MfaOperation.ConfirmEnrollment)).RequireAuthorization();
        group.MapPost("/mfa/recovery-codes", (HttpContext context, IAntiforgery csrf, IAccountMfaService mfa) =>
            Manage(context, csrf, mfa, MfaOperation.RegenerateRecoveryCodes)).RequireAuthorization();
    }

    private static async Task<IResult> Manage(HttpContext context, IAntiforgery csrf, IAccountMfaService mfa, MfaOperation operation)
    {
        if (!await AccountEndpoints.ValidateCsrf(context, csrf)) { return Results.BadRequest(); }
        if (!context.Request.HasJsonContentType()) { return Results.StatusCode(415); }
        Request? body;
        try { body = await context.Request.ReadFromJsonAsync<Request>(context.RequestAborted); }
        catch (System.Text.Json.JsonException) { return Results.BadRequest(); }
        catch (BadHttpRequestException) { return Results.StatusCode(413); }
        if (body?.Password is null || body.Password.Length > 512 || body.Code?.Length > 6
            || body.RecoveryCode?.Length > 64) { return Results.BadRequest(); }
        if (context.Connection.RemoteIpAddress is not { } origin) { return Results.StatusCode(503); }
        var decision = await mfa.ManageAsync(context.User, operation, body.Password, body.Code, body.RecoveryCode,
            origin, context.RequestAborted);
        if (decision.Outcome == LoginOutcome.Throttled)
        {
            context.Response.Headers.RetryAfter = "60";
            return Results.StatusCode(429);
        }
        if (decision.Outcome != LoginOutcome.Accepted) { return Results.BadRequest(); }
        if (decision.RecoveryCodes is not null)
        {
            // Stamp changed: all pre-existing sessions are invalid. Do not silently upgrade this session.
            await context.SignOutAsync(AccountSessionClaims.Scheme);
            return Results.Ok(new { recoveryCodes = decision.RecoveryCodes });
        }
        if (decision.Stamp is not null)
        {
            // Do not SignOut then SignIn in the same request: the cookie handler retains its old store key,
            // and Renew must never resurrect the removed ticket. Require a new verified login instead.
            await context.SignOutAsync(AccountSessionClaims.Scheme);
        }
        // Secret is deliberately returned once to the authenticated, freshly reauthenticated owner over HTTPS.
        // No QR service/network call, no GET, no secret in a URL or log.
        return Results.Ok(new
        {
            sharedKey = decision.Secret,
            expiresInSeconds = 600,
            issuer = "Agentilio",
            requiresSignIn = decision.Stamp is not null
        });
    }

    private sealed class Request
    {
        public string? Password { get; init; }
        public string? Code { get; init; }
        public string? RecoveryCode { get; init; }
    }
}
