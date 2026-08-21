using System.Security.Claims;

using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;

using Microsoft.AspNetCore.Authorization;

namespace HealthTracker.Web.Authentication
{
    public sealed class ActiveAllowedUserRequirement : IAuthorizationRequirement;

    public sealed class AdministratorRequirement : IAuthorizationRequirement;

    public sealed class AccessControlAuthorizationHandler(IHealthDataStore dataStore)
        : AuthorizationHandler<IAuthorizationRequirement>
    {
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            IAuthorizationRequirement requirement
        )
        {
            var allowedUser = await FindActiveUserAsync(context.User, CancellationToken.None);
            if (requirement is ActiveAllowedUserRequirement && allowedUser is not null)
            {
                context.Succeed(requirement);
            }
            if (
                requirement is AdministratorRequirement
                && allowedUser is { Role: AllowedUserRole.Admin }
            )
            {
                context.Succeed(requirement);
            }
        }

        private Task<AllowedUser?> FindActiveUserAsync(ClaimsPrincipal user, CancellationToken ct)
        {
            var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
            return string.IsNullOrWhiteSpace(email)
                ? Task.FromResult<AllowedUser?>(null)
                : dataStore.FindAllowedUserByEmailAsync(email.Trim().ToUpperInvariant(), false, ct);
        }
    }
}
