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
        private const int FailureCueThreshold = 5;
        private static readonly TimeSpan FailureCueWindow = TimeSpan.FromMinutes(15);

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

        public Task RecordForCurrentUserAsync(
            AccessActivityType type,
            string? sourceIpAddress,
            string? userAgent,
            CancellationToken ct
        ) => RecordForCurrentUserAsync(
            type,
            AccessActivityOutcome.Success,
            null,
            sourceIpAddress,
            userAgent,
            ct
        );

        public async Task RecordForCurrentUserAsync(
            AccessActivityType type,
            AccessActivityOutcome outcome,
            AccessActivityFailureReason? failureReason,
            string? sourceIpAddress,
            string? userAgent,
            CancellationToken ct
        )
        {
            var normalizedEmail = NormalizeEmail(currentUser.Email);
            var allowedUser = string.IsNullOrEmpty(normalizedEmail)
                ? null
                : await dataStore.FindAllowedUserByEmailAsync(normalizedEmail, false, ct);
            if (allowedUser is null && outcome == AccessActivityOutcome.Success)
            {
                throw new UnauthorizedAccessException();
            }

            await RecordAsync(
                new RecordAccessActivityDto(
                    allowedUser?.Id,
                    type,
                    outcome,
                    failureReason,
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
            string range,
            string? sourceIpAddress,
            CancellationToken ct
        )
        {
            await RequireAdministratorAsync(ct);
            if (page < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(page));
            }

            var window = CreateWindow(range, DateTimeOffset.UtcNow);
            var normalizedIpAddress = NormalizeFilterIpAddress(sourceIpAddress);
            pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
            var result = await dataStore.GetAccessActivitiesPageAsync(
                allowedUserId,
                type,
                outcome,
                window.FromUtc,
                window.ToUtc,
                normalizedIpAddress,
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

        public async Task<AccessActivitySummaryDto> GetSummaryAsync(string range, CancellationToken ct)
        {
            await RequireAdministratorAsync(ct);
            var window = CreateWindow(range, DateTimeOffset.UtcNow);
            var activities = (await dataStore.GetAccessActivitiesForSummaryAsync(ct))
                .Where(activity => activity.OccurredUtc >= window.FromUtc && activity.OccurredUtc < window.ToUtc)
                .ToArray();

            var timeline = Enumerable.Range(0, window.BucketCount)
                .Select(index =>
                {
                    var bucketStart = window.FromUtc.AddTicks(window.BucketSpan.Ticks * index);
                    var bucketEnd = bucketStart + window.BucketSpan;
                    var bucketActivities = activities.Where(activity =>
                        activity.OccurredUtc >= bucketStart && activity.OccurredUtc < bucketEnd
                    );
                    return new AccessActivityTimelineBucketDto(
                        bucketStart,
                        bucketActivities.Count(activity => activity.Outcome == AccessActivityOutcome.Success),
                        bucketActivities.Count(activity => activity.Outcome == AccessActivityOutcome.Failure)
                    );
                })
                .ToArray();
            var reviewCues = CreateReviewCues(activities);

            return new AccessActivitySummaryDto(
                window.Range,
                window.FromUtc,
                window.ToUtc,
                activities.Length,
                activities.Count(activity => activity.Type == AccessActivityType.WebSignIn && activity.Outcome == AccessActivityOutcome.Success),
                activities.Count(activity => activity.Type == AccessActivityType.WebSignIn && activity.Outcome == AccessActivityOutcome.Failure),
                activities.Count(activity => activity.Type == AccessActivityType.AndroidAuthorization && activity.Outcome == AccessActivityOutcome.Success),
                activities.Count(activity => activity.Type == AccessActivityType.AndroidAuthorization && activity.Outcome == AccessActivityOutcome.Failure),
                activities.Where(activity => !string.IsNullOrEmpty(activity.SourceIpAddress))
                    .Select(activity => activity.SourceIpAddress!)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                activities.Where(activity => activity.AllowedUserId.HasValue)
                    .Select(activity => activity.AllowedUserId!.Value)
                    .Distinct()
                    .Count(),
                activities.Where(activity => activity.Type == AccessActivityType.WebSignIn
                        && activity.Outcome == AccessActivityOutcome.Success
                        && activity.AllowedUserId.HasValue)
                    .Select(activity => activity.AllowedUserId!.Value)
                    .Distinct()
                    .Count(),
                timeline,
                reviewCues
            );
        }

        private static IReadOnlyCollection<AccessActivityReviewCueDto> CreateReviewCues(
            IReadOnlyCollection<AccessActivity> activities
        )
        {
            var cues = new List<AccessActivityReviewCueDto>();
            foreach (var ipGroup in activities
                .Where(activity => activity.Outcome == AccessActivityOutcome.Failure && !string.IsNullOrEmpty(activity.SourceIpAddress))
                .GroupBy(activity => activity.SourceIpAddress!, StringComparer.Ordinal))
            {
                var failures = ipGroup.OrderBy(activity => activity.OccurredUtc).ToArray();
                var right = 0;
                var bestStart = 0;
                var bestEnd = 0;
                var bestCount = 0;
                for (var left = 0; left < failures.Length; left++)
                {
                    if (right < left)
                    {
                        right = left;
                    }

                    var limit = failures[left].OccurredUtc + FailureCueWindow;
                    while (right < failures.Length && failures[right].OccurredUtc <= limit)
                    {
                        right++;
                    }

                    var count = right - left;
                    if (count >= FailureCueThreshold
                        && (count > bestCount || (count == bestCount && failures[right - 1].OccurredUtc > failures[bestEnd - 1].OccurredUtc)))
                    {
                        bestStart = left;
                        bestEnd = right;
                        bestCount = count;
                    }
                }

                if (bestCount < FailureCueThreshold)
                {
                    continue;
                }

                var windowEnd = failures[bestEnd - 1].OccurredUtc;
                var success = activities
                    .Where(activity => activity.SourceIpAddress == ipGroup.Key
                        && activity.Type == AccessActivityType.WebSignIn
                        && activity.Outcome == AccessActivityOutcome.Success)
                    .Where(activity => activity.OccurredUtc > windowEnd && activity.OccurredUtc <= windowEnd.AddHours(1))
                    .OrderBy(activity => activity.OccurredUtc)
                    .FirstOrDefault();
                cues.Add(new AccessActivityReviewCueDto(
                    ipGroup.Key,
                    bestCount,
                    failures[bestStart].OccurredUtc,
                    windowEnd,
                    success is not null,
                    success?.OccurredUtc
                ));
            }

            return cues.OrderByDescending(cue => cue.WindowEndUtc).ToArray();
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

        private static (string Range, DateTimeOffset FromUtc, DateTimeOffset ToUtc, int BucketCount, TimeSpan BucketSpan) CreateWindow(
            string range,
            DateTimeOffset nowUtc
        )
        {
            var (normalizedRange, duration, bucketCount) = range.Trim().ToLowerInvariant() switch
            {
                "24h" => ("24h", TimeSpan.FromHours(24), 24),
                "7d" => ("7d", TimeSpan.FromDays(7), 7),
                _ => throw new ArgumentException("The activity range must be 24h or 7d.", nameof(range)),
            };
            var toUtc = nowUtc.ToUniversalTime();
            return (normalizedRange, toUtc - duration, toUtc, bucketCount, TimeSpan.FromTicks(duration.Ticks / bucketCount));
        }

        private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

        private static string? NormalizeIpAddress(string? value)
        {
            return IPAddress.TryParse(value, out var address) ? address.ToString() : null;
        }

        private static string? NormalizeFilterIpAddress(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return IPAddress.TryParse(value.Trim(), out var address)
                ? address.ToString()
                : throw new ArgumentException("The source IP address is invalid.", nameof(value));
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
