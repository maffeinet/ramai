using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Ramai.Application.Contracts.Identity;

namespace Ramai.Api.Authentication;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/account");
        group.MapGet("/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(new { requestToken = antiforgery.GetAndStoreTokens(context).RequestToken }));
        group.MapPost("/login", Login);
        group.MapMfaEndpoints();
        group.MapGet("/session", (ClaimsPrincipal user) =>
            Results.Ok(new { userId = user.FindFirstValue(ClaimTypes.NameIdentifier) })).RequireAuthorization();
        group.MapPost("/logout", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await ValidateCsrf(context, antiforgery)) { return Results.StatusCode(400); }
            await context.SignOutAsync(AccountSessionClaims.Scheme);
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Ramai.AccountSessions")
                .LogInformation("Account session operation {Operation} outcome {Outcome}", "Logout", "Completed");
            return Results.NoContent();
        }).RequireAuthorization();
        group.MapPost("/revoke-all", async (HttpContext context, IAntiforgery antiforgery, IAccountSessionValidator sessions) =>
        {
            if (!await ValidateCsrf(context, antiforgery)) { return Results.StatusCode(400); }
            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                || !await sessions.RevokeAllAsync(id, context.RequestAborted)) { return Results.StatusCode(503); }
            await context.SignOutAsync(AccountSessionClaims.Scheme);
            context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Ramai.AccountSessions")
                .LogInformation("Account session operation {Operation} outcome {Outcome}", "RevokeAll", "Completed");
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static async Task<IResult> Login(HttpContext context, IAntiforgery antiforgery, IAccountLoginService accounts)
    {
        if (!await ValidateCsrf(context, antiforgery)) { return Results.StatusCode(400); }
        // Avoid session fixation/account switching through a second login on an authenticated session.
        if (context.User.Identity?.IsAuthenticated == true) { return Results.StatusCode(409); }
        if (!context.Request.HasJsonContentType()) { return Results.StatusCode(415); }
        LoginRequest? request;
        try { request = await context.Request.ReadFromJsonAsync<LoginRequest>(context.RequestAborted); }
        catch (System.Text.Json.JsonException) { return Results.BadRequest(); }
        catch (BadHttpRequestException) { return Results.StatusCode(413); }
        if (request is null || request.Email is null || request.Password is null || request.Email.Length > 256
            || request.Password.Length > 512 || request.Code?.Length > 6 || request.RecoveryCode?.Length > 64) { return Results.BadRequest(); }
        var origin = context.Connection.RemoteIpAddress;
        if (origin is null) { return Results.StatusCode(503); }
        var decision = await accounts.CheckAsync(request.Email, request.Password, origin,
            context.TraceIdentifier, context.RequestAborted, request.Code, request.RecoveryCode);
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Ramai.AccountSessions")
            .LogInformation("Account session operation {Operation} outcome {Outcome}", "Login", decision.Outcome);
        if (decision.Outcome == LoginOutcome.Throttled)
        {
            context.Response.Headers.RetryAfter = "60";
            return Results.StatusCode(429);
        }
        if (decision.Outcome == LoginOutcome.Unavailable) { return Results.StatusCode(503); }
        if (decision.Outcome != LoginOutcome.Accepted || decision.Principal is null)
        {
            return Results.Problem(statusCode: 401, title: "Unable to sign in with the supplied credentials.");
        }
        await context.SignInAsync(AccountSessionClaims.Scheme, decision.Principal,
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
        return Results.NoContent();
    }

    internal static async Task<bool> ValidateCsrf(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private sealed class LoginRequest
    {
        public string? Email { get; init; }
        public string? Password { get; init; }
        public string? Code { get; init; }
        public string? RecoveryCode { get; init; }
    }
}
