using HealthTracker.Application.Dtos;
using HealthTracker.Application.Services;
using HealthTracker.Domain.Models;
using HealthTracker.Web.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthTracker.Web.Controllers
{
    [ApiController, Authorize(Policy = "Administrator"), Route("api/admin/diagnostics")]
    public sealed class DiagnosticsController(
        ApplicationLogService logs,
        AccessActivityService accessActivity
    ) : ControllerBase
    {
        [HttpGet("logs")]
        public Task<ApplicationLogSnapshot> GetLogs(CancellationToken ct) =>
            logs.ReadForViewerAsync(ct);

        [HttpGet("activity")]
        public async Task<ActionResult<AccessActivityPageDto>> GetActivity(
            [FromQuery] Guid? userId,
            [FromQuery] AccessActivityType? type,
            [FromQuery] AccessActivityOutcome? outcome,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string range = "24h",
            [FromQuery] string? sourceIpAddress = null,
            CancellationToken ct = default
        )
        {
            try
            {
                return Ok(await accessActivity.GetPageAsync(
                    userId,
                    type,
                    outcome,
                    page,
                    pageSize,
                    range,
                    sourceIpAddress,
                    ct
                ));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails { Detail = ex.Message });
            }
        }

        [HttpGet("activity/summary")]
        public async Task<ActionResult<AccessActivitySummaryDto>> GetActivitySummary(
            [FromQuery] string range = "24h",
            CancellationToken ct = default
        )
        {
            try
            {
                return Ok(await accessActivity.GetSummaryAsync(range, ct));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails { Detail = ex.Message });
            }
        }
    }
}
