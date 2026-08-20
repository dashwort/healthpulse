using System.Security.Claims;

using HealthTracker.Application.Abstractions;

namespace HealthTracker.Web.Authentication
{
    public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
    {
        private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        public string Subject =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;
        public string DisplayName =>
            User.Identity?.Name ?? User.FindFirstValue("name") ?? "HealthPulse user";
    }
}
