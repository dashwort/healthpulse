using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HealthTracker.Web.Security
{
    /// <summary>
    /// Validates unsafe API requests made with the production browser cookie while leaving the
    /// Android bearer API and anonymous mobile token exchange independent of browser antiforgery.
    /// </summary>
    public sealed class CookieAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var method = context.HttpContext.Request.Method;
            if (
                HttpMethods.IsGet(method)
                || HttpMethods.IsHead(method)
                || HttpMethods.IsOptions(method)
                || HttpMethods.IsTrace(method)
                || !context.HttpContext.User.Identities.Any(
                    identity =>
                        identity.IsAuthenticated
                        && string.Equals(
                            identity.AuthenticationType,
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            StringComparison.Ordinal
                        )
                )
            )
            {
                return;
            }

            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new BadRequestObjectResult(
                    new ProblemDetails { Detail = "The antiforgery token is invalid." }
                );
            }
        }
    }
}
