using HealthTracker.Application.Services;
using HealthTracker.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTracker.Web.Controllers
{
    [ApiController, Authorize, Route("api/templates")]
    public sealed class TemplatesController(HealthTrackerService service) : ControllerBase
    {
        [HttpGet("catalogue")]
        public async Task<ActionResult<IReadOnlyCollection<TemplateResponse>>> GetCatalogue(
            CancellationToken ct
        )
        {
            return Ok((await service.GetCatalogueAsync(ct)).Select(x => x.ToResponse()));
        }

        [HttpGet("tracked")]
        public async Task<ActionResult<IReadOnlyCollection<TemplateResponse>>> GetTracked(
            CancellationToken ct
        )
        {
            return Ok((await service.GetTrackedTemplatesAsync(ct)).Select(x => x.ToResponse()));
        }

        [HttpPost("custom")]
        public async Task<ActionResult<TemplateResponse>> CreateCustom(
            CustomTemplateRequest request,
            CancellationToken ct
        )
        {
            return await ExecuteAsync<HealthTracker.Application.Dtos.TemplateDto, TemplateResponse>(
                () => service.CreateCustomTemplateAsync(request.ToCreateDto(), ct),
                result => CreatedAtAction(nameof(GetTracked), result.ToResponse())
            );
        }

        [HttpPut("custom/{templateId:guid}")]
        public async Task<ActionResult<TemplateResponse>> UpdateCustom(
            Guid templateId,
            CustomTemplateRequest request,
            CancellationToken ct
        )
        {
            return await ExecuteAsync<HealthTracker.Application.Dtos.TemplateDto, TemplateResponse>(
                () => service.UpdateCustomTemplateAsync(templateId, request.ToUpdateDto(), ct),
                result => Ok(result.ToResponse())
            );
        }

        [HttpDelete("custom/{templateId:guid}")]
        public async Task<IActionResult> DeleteCustom(Guid templateId, CancellationToken ct)
        {
            return await ExecuteAsync(async () =>
            {
                await service.DeleteCustomTemplateAsync(templateId, ct);
                return NoContent();
            });
        }

        [HttpPost("{templateId:guid}/track")]
        public async Task<IActionResult> Track(Guid templateId, CancellationToken ct)
        {
            return await ExecuteAsync(async () =>
            {
                await service.TrackTemplateAsync(templateId, ct);
                return NoContent();
            });
        }

        [HttpDelete("{templateId:guid}/track")]
        public async Task<IActionResult> StopTracking(Guid templateId, CancellationToken ct)
        {
            return await ExecuteAsync(async () =>
            {
                await service.StopTrackingAsync(templateId, ct);
                return NoContent();
            });
        }

        private async Task<ActionResult<TResult>> ExecuteAsync<T, TResult>(
            Func<Task<T>> action,
            Func<T, ActionResult<TResult>> response
        )
        {
            try
            {
                return response(await action());
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

        private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
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
    }
}
