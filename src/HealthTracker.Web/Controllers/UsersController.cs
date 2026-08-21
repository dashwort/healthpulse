using HealthTracker.Application.Dtos;
using HealthTracker.Application.Services;
using HealthTracker.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTracker.Web.Controllers
{
    [ApiController, Authorize(Policy = "Administrator"), Route("api/users")]
    public sealed class UsersController(HealthTrackerService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyCollection<AllowedUserResponse>>> Get(
            [FromQuery] bool includeArchived = false,
            CancellationToken ct = default
        )
        {
            return Ok(
                (await service.GetAllowedUsersAsync(includeArchived, ct)).Select(x => x.ToResponse())
            );
        }

        [HttpPost]
        public async Task<ActionResult<AllowedUserResponse>> Add(
            AllowedUserRequest request,
            CancellationToken ct
        )
        {
            return await ExecuteAsync(
                () => service.AddAllowedUserAsync(new AddAllowedUserDto(request.Email, request.Role), ct),
                result => CreatedAtAction(nameof(Get), result.ToResponse())
            );
        }

        [HttpPut("{allowedUserId:guid}/role")]
        public async Task<ActionResult<AllowedUserResponse>> UpdateRole(
            Guid allowedUserId,
            AllowedUserRoleRequest request,
            CancellationToken ct
        )
        {
            return await ExecuteAsync(
                () =>
                    service.UpdateAllowedUserRoleAsync(
                        allowedUserId,
                        new UpdateAllowedUserRoleDto(request.Role),
                        ct
                    ),
                result => Ok(result.ToResponse())
            );
        }

        [HttpDelete("{allowedUserId:guid}")]
        public async Task<IActionResult> Archive(Guid allowedUserId, CancellationToken ct)
        {
            try
            {
                await service.ArchiveAllowedUserAsync(allowedUserId, ct);
                return NoContent();
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

        private async Task<ActionResult<AllowedUserResponse>> ExecuteAsync(
            Func<Task<AllowedUserDto>> action,
            Func<AllowedUserDto, ActionResult<AllowedUserResponse>> response
        )
        {
            try
            {
                return response(await action());
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails { Detail = ex.Message });
            }
        }
    }
}
