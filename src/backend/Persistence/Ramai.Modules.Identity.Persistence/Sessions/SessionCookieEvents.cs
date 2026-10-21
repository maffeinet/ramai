using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using Ramai.Application.Contracts.Identity;

namespace Ramai.Modules.Identity.Persistence.Sessions;

public sealed class SessionCookieEvents(IAccountSessionValidator validator, TimeProvider clock,
    ILogger<SessionCookieEvents> logger) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var validTime = long.TryParse(context.Principal?.FindFirst(AccountSessionClaims.Started)?.Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            && start >= 0 && start <= clock.GetUtcNow().ToUnixTimeSeconds()
            && clock.GetUtcNow().ToUnixTimeSeconds() - start < (long)RedisSessionTicketStore.Absolute.TotalSeconds;
        if (!validTime || context.Principal is null ||
            !await validator.ValidateAsync(context.Principal, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            logger.LogInformation("Account session validation outcome {Outcome}", "Rejected");
        }
        else { context.ShouldRenew = true; }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}
