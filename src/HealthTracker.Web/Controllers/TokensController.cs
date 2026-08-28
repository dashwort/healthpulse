using HealthTracker.Application.Dtos;
using HealthTracker.Application.Services;
using HealthTracker.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTracker.Web.Controllers
{
    [ApiController, Authorize(Policy = "InteractiveUser"), Route("api/tokens")]
    public sealed class TokensController(PersonalAccessTokenService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyCollection<PersonalAccessTokenResponse>>> Get(
            CancellationToken ct
        ) => Ok((await service.GetTokensAsync(ct)).Select(ToResponse));

        [HttpPost]
        public async Task<ActionResult<CreatedPersonalAccessTokenResponse>> Create(
            PersonalAccessTokenRequest request,
            CancellationToken ct
        )
        {
            try
            {
                var created = await service.CreateTokenAsync(request.Name, ct);
                return Created(
                    "/api/tokens",
                    new CreatedPersonalAccessTokenResponse(ToResponse(created.Token), created.Secret)
                );
            }
            catch (InvalidOperationException exception)
            {
                return BadRequest(new ProblemDetails { Detail = exception.Message });
            }
        }

        [HttpDelete("{tokenId:guid}")]
        public Task<IActionResult> Revoke(Guid tokenId, CancellationToken ct) =>
            RevokeAsync(tokenId, null, ct);

        [HttpGet("users/{allowedUserId:guid}"), Authorize(Policy = "Administrator")]
        public async Task<ActionResult<IReadOnlyCollection<PersonalAccessTokenResponse>>> GetForUser(
            Guid allowedUserId,
            CancellationToken ct
        ) => Ok((await service.GetTokensForUserAsync(allowedUserId, ct)).Select(ToResponse));

        [HttpDelete("users/{allowedUserId:guid}/{tokenId:guid}"), Authorize(Policy = "Administrator")]
        public Task<IActionResult> RevokeForUser(
            Guid allowedUserId,
            Guid tokenId,
            CancellationToken ct
        ) => RevokeAsync(tokenId, allowedUserId, ct);

        private async Task<IActionResult> RevokeAsync(
            Guid tokenId,
            Guid? allowedUserId,
            CancellationToken ct
        )
        {
            try
            {
                await service.RevokeTokenAsync(tokenId, allowedUserId, ct);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        private static PersonalAccessTokenResponse ToResponse(PersonalAccessTokenDto token) =>
            new(
                token.Id,
                token.Name,
                token.Prefix,
                token.ExpiresUtc,
                token.LastUsedUtc,
                token.IsRevoked
            );
    }
}
