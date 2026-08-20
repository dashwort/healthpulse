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
}
