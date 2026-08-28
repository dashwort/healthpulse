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
        public Task<AccessActivityPageDto> GetActivity(
            [FromQuery] Guid? userId,
            [FromQuery] AccessActivityType? type,
            [FromQuery] AccessActivityOutcome? outcome,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken ct = default
        ) => accessActivity.GetPageAsync(userId, type, outcome, page, pageSize, ct);
    }
}
