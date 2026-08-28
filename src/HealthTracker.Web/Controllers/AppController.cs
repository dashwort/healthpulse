using HealthTracker.Application.Abstractions;
using HealthTracker.Web.Configuration;
using HealthTracker.Web.Models;
using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HealthTracker.Web.Controllers
{
    [ApiController, Route("api/app")]
    public sealed class AppController(
        ICurrentUser currentUser,
        IAuthorizationService authorization,
        IAntiforgery antiforgery,
        IOptions<DeploymentInfo> deployment,
        MobileReleaseService mobileReleaseService
    ) : ControllerBase
    {
        [HttpGet("session"), AllowAnonymous]
        public async Task<ActionResult<SessionResponse>> GetSession()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Ok(new SessionResponse(false, null, null, false, null));
            }

            var administrator = await authorization.AuthorizeAsync(User, "Administrator");
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(
                new SessionResponse(
                    true,
                    currentUser.DisplayName,
                    currentUser.Email,
                    administrator.Succeeded,
                    tokens.RequestToken
                )
            );
        }

        [HttpGet("info"), Authorize(Policy = "InteractiveUser")]
        public async Task<ActionResult<AppInfoResponse>> GetInfo(CancellationToken ct)
        {
            var info = deployment.Value;
            var android = await mobileReleaseService.GetLatestAsync(ct);
            return Ok(
                new AppInfoResponse(
                    new DeploymentResponse(info.Version, info.Build, info.Commit, info.BuiltAtUtc),
                    new AndroidReleaseResponse(
                        android.LatestVersion,
                        android.ApkUrl,
                        android.ReleaseNotes
                    )
                )
            );
        }
    }
}
