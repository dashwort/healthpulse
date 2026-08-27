using HealthTracker.Application.Dtos;
using HealthTracker.Domain.Models;

namespace HealthTracker.Web.Services
{
    /// <summary>Translates trusted HTTP connection metadata into an application audit request.</summary>
    public static class AccessActivityRequestDetails
    {
        public static RecordAccessActivityDto Create(
            HttpContext context,
            Guid? allowedUserId,
            AccessActivityType type,
            AccessActivityOutcome outcome,
            AccessActivityFailureReason? failureReason = null
        )
        {
            return new RecordAccessActivityDto(
                allowedUserId,
                type,
                outcome,
                failureReason,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString()
            );
        }
    }
}
