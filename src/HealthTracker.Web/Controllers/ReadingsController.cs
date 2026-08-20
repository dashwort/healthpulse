using HealthTracker.Application.Services;
using HealthTracker.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTracker.Web.Controllers
{
    [ApiController, Authorize, Route("api/readings")]
    public sealed class ReadingsController(HealthTrackerService service) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<ReadingPageResponse>> Get(
            [FromQuery] Guid? templateId,
            [FromQuery] DateTimeOffset? fromUtc,
            [FromQuery] DateTimeOffset? toUtc,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default
        )
        {
            try
            {
                var result = await service.GetReadingPageAsync(
                    templateId,
                    fromUtc,
                    toUtc,
                    page,
                    pageSize,
                    ct
                );
                return Ok(
                    new ReadingPageResponse(
                        [.. result.Items.Select(x => x.ToResponse())],
                        result.TotalCount,
                        result.Page,
                        result.PageSize
                    )
                );
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails { Detail = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ReadingResponse>> Create(
            ReadingRequest request,
            CancellationToken ct
        )
        {
            return await ExecuteAsync<HealthTracker.Application.Dtos.ReadingDto, ReadingResponse>(
                () => service.CreateReadingAsync(request.ToCreateDto(), ct),
                result => CreatedAtAction(nameof(Get), new { result.Id }, result.ToResponse())
            );
        }

        [HttpPut("{readingId:guid}")]
        public async Task<ActionResult<ReadingResponse>> Update(
            Guid readingId,
            UpdateReadingRequest request,
            CancellationToken ct
        )
        {
            return await ExecuteAsync<HealthTracker.Application.Dtos.ReadingDto, ReadingResponse>(
                () => service.UpdateReadingAsync(readingId, request.ToUpdateDto(), ct),
                result => Ok(result.ToResponse())
            );
        }

        [HttpDelete("{readingId:guid}")]
        public async Task<IActionResult> Delete(Guid readingId, CancellationToken ct)
        {
            try
            {
                await service.DeleteReadingAsync(readingId, ct);
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
    }
}
