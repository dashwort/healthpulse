using System.ComponentModel.DataAnnotations;

using HealthTracker.Application.Services;
using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace HealthTracker.Web.Controllers
{
    [ApiController, Route("api/mobile/auth")]
    public sealed class MobileAuthenticationController(
        MobileAuthenticationService mobileAuthentication,
        HealthTrackerService healthTrackerService
    ) : ControllerBase
    {
        [AllowAnonymous, HttpGet("authorize")]
        public async Task<IActionResult> Authorize(
            [FromQuery] MobileAuthorizeRequest request,
            CancellationToken ct
        )
        {
            try
            {
                var authorizationRequest = await mobileAuthentication.BeginAsync(
                    request.CodeChallenge,
                    request.State,
                    request.RedirectUri,
                    ct
                );
                var completeUrl = Url.ActionLink(
                    nameof(Complete),
                    values: new { requestId = authorizationRequest.Id }
                )!;
                if (User.Identity?.IsAuthenticated == true)
                {
                    return Redirect(completeUrl);
                }

                return Challenge(
                    new AuthenticationProperties
                    {
                        RedirectUri = completeUrl,
                    }
                );
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails { Detail = ex.Message });
            }
        }

        [Authorize(Policy = "InteractiveUser"), HttpGet("complete")]
        public async Task<IActionResult> Complete(Guid requestId, CancellationToken ct)
        {
            try
            {
                var user = await healthTrackerService.EnsureCurrentUserAsync(ct);
                var (request, authorizationCode) = await mobileAuthentication.CompleteAsync(
                    requestId,
                    user.Id,
                    ct
                );
                return Redirect(
                    QueryHelpers.AddQueryString(
                        request.RedirectUri,
                        new Dictionary<string, string?>
                        {
                            ["code"] = authorizationCode,
                            ["state"] = request.State,
                        }
                    )
                );
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails { Detail = ex.Message });
            }
        }

        [AllowAnonymous, HttpPost("token")]
        public async Task<ActionResult<MobileTokenResponse>> Token(
            MobileTokenRequest request,
            CancellationToken ct
        )
        {
            try
            {
                var token = request.GrantType switch
                {
                    "authorization_code" => await mobileAuthentication.ExchangeAuthorizationCodeAsync(
                        request.Code ?? string.Empty,
                        request.CodeVerifier ?? string.Empty,
                        ct
                    ),
                    "refresh_token" => await mobileAuthentication.RefreshAsync(
                        request.RefreshToken ?? string.Empty,
                        ct
                    ),
                    _ => throw new InvalidOperationException("The token request is invalid."),
                };
                return Ok(
                    new MobileTokenResponse(
                        token.AccessToken,
                        token.RefreshToken,
                        token.AccessTokenExpiresUtc,
                        "Bearer"
                    )
                );
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails { Detail = ex.Message });
            }
        }
    }

    public sealed class MobileAuthorizeRequest
    {
        [Required, StringLength(128, MinimumLength = 43)]
        [FromQuery(Name = "code_challenge")]
        public string CodeChallenge { get; init; } = string.Empty;

        [Required, StringLength(512)]
        public string State { get; init; } = string.Empty;

        [Required, StringLength(500)]
        [FromQuery(Name = "redirect_uri")]
        public string RedirectUri { get; init; } = string.Empty;
    }

    public sealed class MobileTokenRequest
    {
        [Required]
        public string GrantType { get; init; } = string.Empty;

        [StringLength(512)]
        public string? Code { get; init; }

        [StringLength(128)]
        public string? CodeVerifier { get; init; }

        [StringLength(512)]
        public string? RefreshToken { get; init; }
    }

    public sealed record MobileTokenResponse(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset AccessTokenExpiresUtc,
        string TokenType
    );
}
