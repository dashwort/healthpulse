using System.Security.Claims;
using System.Text.Encodings.Web;

using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HealthTracker.Web.Authentication
{
    public sealed class PersonalAccessTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHealthDataStore dataStore
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorization = Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return AuthenticateResult.NoResult();
            }

            var token = await dataStore.FindActiveTokenByHashAsync(
                PersonalAccessTokenService.Hash(authorization[7..].Trim()),
                Context.RequestAborted
            );
            var user = token is null
                ? null
                : await dataStore.FindAllowedUserByIdAsync(token.AllowedUserId, false, Context.RequestAborted);
            if (token is null || user is null)
            {
                return AuthenticateResult.Fail("Invalid access token.");
            }

            var applicationUser = user.ApplicationUserId.HasValue
                ? await dataStore.FindUserByIdAsync(user.ApplicationUserId.Value, Context.RequestAborted)
                : null;
            if (applicationUser is null)
            {
                return AuthenticateResult.Fail("Invalid access token.");
            }

            token.LastUsedUtc = DateTimeOffset.UtcNow;
            await dataStore.UpdateTokenAsync(token, Context.RequestAborted);
            await dataStore.SaveChangesAsync(Context.RequestAborted);
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, applicationUser.Subject),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Email),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim("personal_access_token_id", token.Id.ToString()),
                ],
                Scheme.Name
            );
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }
    }
}
