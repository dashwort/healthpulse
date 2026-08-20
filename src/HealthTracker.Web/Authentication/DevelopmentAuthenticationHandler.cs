using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HealthTracker.Web.Authentication
{
    public sealed class DevelopmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "development-user"),
                    new Claim(ClaimTypes.Name, "Development user"),
                ],
                Scheme.Name
            );
            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)
                )
            );
        }
    }
}
