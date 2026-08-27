using System.Net;

using HealthTracker.Application.Abstractions;
using HealthTracker.Application.Dtos;
using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Services
{
    /// <summary>Application use cases for the administrator-visible authentication activity trail.</summary>
    public sealed class AccessActivityService(IHealthDataStore dataStore, ICurrentUser currentUser)
    {
        private const int MaximumPageSize = 100;
        private const int MaximumUserAgentLength = 512;

        public async Task RecordAsync(RecordAccessActivityDto request, CancellationToken ct)
        {
            if (!Enum.IsDefined(request.Type) || !Enum.IsDefined(request.Outcome))
            {
                throw new ArgumentException("The access activity type or outcome is invalid.");
            }

            if (request.FailureReason.HasValue && !Enum.IsDefined(request.FailureReason.Value))
            {
                throw new ArgumentException("The access activity failure reason is invalid.");
            }

            if (request.Outcome == AccessActivityOutcome.Success && request.FailureReason is not null)
            {
                throw new ArgumentException("Successful access activity cannot have a failure reason.");
            }

            if (request.Outcome == AccessActivityOutcome.Failure && request.FailureReason is null)
            {
                throw new ArgumentException("Failed access activity requires a safe failure reason.");
            }

            await dataStore.AddAccessActivityAsync(
                new AccessActivity
                {
                    AllowedUserId = request.AllowedUserId,
                    Type = request.Type,
                    Outcome = request.Outcome,
                    FailureReason = request.FailureReason,
                    SourceIpAddress = NormalizeIpAddress(request.SourceIpAddress),
                    UserAgent = NormalizeUserAgent(request.UserAgent),
                },
                ct
            );
            await dataStore.SaveChangesAsync(ct);
        }

        public async Task RecordForCurrentUserAsync(
            AccessActivityType type,
            string? sourceIpAddress,
            string? userAgent,
            CancellationToken ct
        )
        {
            var normalizedEmail = NormalizeEmail(currentUser.Email);
            var allowedUser = string.IsNullOrEmpty(normalizedEmail)
                ? null
                : await dataStore.FindAllowedUserByEmailAsync(normalizedEmail, false, ct);
            if (allowedUser is null)
            {
                throw new UnauthorizedAccessException();
            }

            await RecordAsync(
                new RecordAccessActivityDto(
                    allowedUser.Id,
                    type,
                    AccessActivityOutcome.Success,
                    null,
                    sourceIpAddress,
                    userAgent
                ),
                ct
            );
        }

        public async Task<AccessActivityPageDto> GetPageAsync(
            Guid? allowedUserId,
            AccessActivityType? type,
            AccessActivityOutcome? outcome,
            int page,
            int pageSize,
            CancellationToken ct
        )
        {
            await RequireAdministratorAsync(ct);
            if (page < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(page));
            }

            pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
            var result = await dataStore.GetAccessActivitiesPageAsync(
                allowedUserId,
                type,
                outcome,
                page,
                pageSize,
                ct
            );
            var users = (await dataStore.GetAllowedUsersAsync(true, ct)).ToDictionary(user => user.Id);
            return new AccessActivityPageDto(
                [
                    .. result.Items.Select(activity => new AccessActivityDto(
                        activity.Id,
                        activity.AllowedUserId,
                        activity.AllowedUserId is { } userId && users.TryGetValue(userId, out var user)
                            ? user.Email
                            : null,
                        activity.Type.ToString(),
                        activity.Outcome.ToString(),
                        activity.FailureReason?.ToString(),
                        activity.OccurredUtc,
                        activity.SourceIpAddress,
                        activity.UserAgent
                    )),
                ],
                result.TotalCount,
                page,
                pageSize
            );
        }

        private async Task RequireAdministratorAsync(CancellationToken ct)
        {
            var normalizedEmail = NormalizeEmail(currentUser.Email);
            var allowedUser = string.IsNullOrEmpty(normalizedEmail)
                ? null
                : await dataStore.FindAllowedUserByEmailAsync(normalizedEmail, false, ct);
            if (allowedUser?.Role != AllowedUserRole.Admin)
            {
                throw new UnauthorizedAccessException();
            }
        }

        private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

        private static string? NormalizeIpAddress(string? value)
        {
            return IPAddress.TryParse(value, out var address) ? address.ToString() : null;
        }

        private static string? NormalizeUserAgent(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = new string(value.Trim().Select(character => char.IsControl(character) ? ' ' : character).ToArray());
            return normalized.Length <= MaximumUserAgentLength
                ? normalized
                : normalized[..MaximumUserAgentLength];
        }
    }
}
