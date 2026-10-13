using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Dtos
{
    public sealed record TemplateDto(
        Guid Id,
        string? Code,
        string Name,
        string Category,
        string NormalizedUnit,
        IReadOnlyCollection<string> AllowedUnits,
        bool IsCustom,
        bool IsTracked
    );

    public sealed record CreateCustomTemplateDto(string Name, string Category, string Unit);

    public sealed record UpdateCustomTemplateDto(string Name, string Category, string Unit);

    public sealed record CreateReadingDto(
        Guid TemplateId,
        decimal Value,
        string Unit,
        DateTimeOffset RecordedAtUtc,
        string? Note
    );

    public sealed record UpdateReadingDto(
        decimal Value,
        string Unit,
        DateTimeOffset RecordedAtUtc,
        string? Note
    );

    public sealed record ReadingDto(
        Guid Id,
        Guid TemplateId,
        string TemplateName,
        decimal Value,
        string Unit,
        DateTimeOffset RecordedAtUtc,
        string? Note
    );

    public sealed record ReadingPageDto(
        IReadOnlyCollection<ReadingDto> Items,
        int TotalCount,
        int Page,
        int PageSize
    );

    public sealed record MetricSummaryDto(
        Guid TemplateId,
        string TemplateName,
        decimal LatestValue,
        string Unit,
        DateTimeOffset RecordedAtUtc,
        decimal? ChangeFromPrevious
    );

    public sealed record AllowedUserDto(
        Guid Id,
        string Email,
        string Role,
        bool HasSignedIn,
        DateTimeOffset? FirstSignedInUtc,
        DateTimeOffset? LastSignedInUtc,
        bool IsArchived
    );

    public sealed record AddAllowedUserDto(string Email, string Role);

    public sealed record UpdateAllowedUserRoleDto(string Role);

    public sealed record PersonalAccessTokenDto(Guid Id, string Name, string Prefix, DateTimeOffset ExpiresUtc, DateTimeOffset? LastUsedUtc, bool IsRevoked);

    public sealed record CreatedPersonalAccessTokenDto(PersonalAccessTokenDto Token, string Secret);

    public sealed record RecordAccessActivityDto(
        Guid? AllowedUserId,
        AccessActivityType Type,
        AccessActivityOutcome Outcome,
        AccessActivityFailureReason? FailureReason,
        string? SourceIpAddress,
        string? UserAgent
    );

    public sealed record AccessActivityDto(
        Guid Id,
        Guid? AllowedUserId,
        string? UserEmail,
        string Type,
        string Outcome,
        string? FailureReason,
        DateTimeOffset OccurredUtc,
        string? SourceIpAddress,
        string? UserAgent
    );

    public sealed record AccessActivityPageDto(
        IReadOnlyCollection<AccessActivityDto> Items,
        int TotalCount,
        int Page,
        int PageSize
    );

    public sealed record AccessActivitySummaryDto(
        string Range,
        DateTimeOffset FromUtc,
        DateTimeOffset ToUtc,
        int TotalEvents,
        int SuccessfulWebSignIns,
        int FailedWebSignIns,
        int SuccessfulAndroidAuthorizations,
        int FailedAndroidAuthorizations,
        int DistinctSourceIpCount,
        int DistinctAccountCount,
        int DistinctSuccessfulAccountCount,
        IReadOnlyCollection<AccessActivityTimelineBucketDto> Timeline,
        IReadOnlyCollection<AccessActivityReviewCueDto> ReviewCues
    );

    public sealed record AccessActivityTimelineBucketDto(
        DateTimeOffset StartUtc,
        int SuccessCount,
        int FailureCount
    );

    public sealed record AccessActivityReviewCueDto(
        string SourceIpAddress,
        int FailureCount,
        DateTimeOffset WindowStartUtc,
        DateTimeOffset WindowEndUtc,
        bool SuccessfulSignInAfterBurst,
        DateTimeOffset? SuccessUtc
    );
}
