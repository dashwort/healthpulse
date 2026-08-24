using System.Security.Claims;
using System.Text.Encodings.Web;

using HealthTracker.Application.Abstractions;
using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HealthTracker.Web.Authentication
{
    public sealed class MobileBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHealthDataStore dataStore
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorization = Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer hpma_", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.NoResult();
            }

            var session = await dataStore.FindActiveMobileSessionByAccessHashAsync(
                MobileAuthenticationService.Hash(authorization[7..].Trim()),
                Context.RequestAborted
            );
            if (session is null || session.AccessTokenExpiresUtc <= DateTimeOffset.UtcNow)
            {
                return AuthenticateResult.Fail("Invalid mobile access token.");
            }

            var applicationUser = await dataStore.FindUserByIdAsync(
                session.ApplicationUserId,
                Context.RequestAborted
            );
            var allowedUser = (
                await dataStore.GetAllowedUsersAsync(false, Context.RequestAborted)
            ).SingleOrDefault(x => x.ApplicationUserId == session.ApplicationUserId);
            if (applicationUser is null || allowedUser is null)
            {
                return AuthenticateResult.Fail("Invalid mobile access token.");
            }

            session.LastUsedUtc = DateTimeOffset.UtcNow;
            await dataStore.UpdateMobileSessionAsync(session, Context.RequestAborted);
            await dataStore.SaveChangesAsync(Context.RequestAborted);
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, applicationUser.Subject),
                    new Claim(ClaimTypes.Email, allowedUser.Email),
                    new Claim(ClaimTypes.Name, applicationUser.DisplayName),
                    new Claim(ClaimTypes.Role, allowedUser.Role.ToString()),
                    new Claim("mobile_session_id", session.Id.ToString()),
                ],
                Scheme.Name
            );
            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)
            );
        }
    }
}
