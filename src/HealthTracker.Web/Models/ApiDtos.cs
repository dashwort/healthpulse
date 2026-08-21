using System.ComponentModel.DataAnnotations;

using HealthTracker.Application.Dtos;

namespace HealthTracker.Web.Models
{
    public sealed record AllowedUserRequest(string Email, string Role);

    public sealed record AllowedUserRoleRequest(string Role);

    public sealed record AllowedUserResponse(
        Guid Id,
        string Email,
        string Role,
        bool HasSignedIn,
        DateTimeOffset? FirstSignedInUtc,
        DateTimeOffset? LastSignedInUtc,
        bool IsArchived
    );

    public sealed record TemplateResponse(
        Guid Id,
        string? Code,
        string Name,
        string Category,
        string NormalizedUnit,
        IReadOnlyCollection<string> AllowedUnits,
        bool IsCustom,
        bool IsTracked
    );

    public sealed class CustomTemplateRequest
    {
        [Required, StringLength(100)]
        public string Name { get; init; } = string.Empty;

        [Required, StringLength(100)]
        public string Category { get; init; } = "Custom";

        [Required, StringLength(30)]
        public string Unit { get; init; } = string.Empty;
    }

    public sealed class ReadingRequest
    {
        [Required]
        public Guid TemplateId
        {
            get; init;
        }

        [Range(0, 1_000_000)]
        public decimal Value
        {
            get; init;
        }

        [Required, StringLength(30)]
        public string Unit { get; init; } = string.Empty;
        public DateTimeOffset RecordedAtUtc
        {
            get; init;
        }

        [StringLength(140)]
        public string? Note
        {
            get; init;
        }
    }

    public sealed class UpdateReadingRequest
    {
        [Range(0, 1_000_000)]
        public decimal Value
        {
            get; init;
        }

        [Required, StringLength(30)]
        public string Unit { get; init; } = string.Empty;
        public DateTimeOffset RecordedAtUtc
        {
            get; init;
        }

        [StringLength(140)]
        public string? Note
        {
            get; init;
        }
    }

    public sealed record ReadingResponse(
        Guid Id,
        Guid TemplateId,
        string TemplateName,
        decimal Value,
        string Unit,
        DateTimeOffset RecordedAtUtc,
        string? Note
    );

    public sealed record ReadingPageResponse(
        IReadOnlyCollection<ReadingResponse> Items,
        int TotalCount,
        int Page,
        int PageSize
    );

    public static class ApiDtoMappings
    {
        public static AllowedUserResponse ToResponse(this AllowedUserDto user)
        {
            return new(
                user.Id,
                user.Email,
                user.Role,
                user.HasSignedIn,
                user.FirstSignedInUtc,
                user.LastSignedInUtc,
                user.IsArchived
            );
        }

        public static CreateCustomTemplateDto ToCreateDto(this CustomTemplateRequest request)
        {
            return new(request.Name, request.Category, request.Unit);
        }

        public static UpdateCustomTemplateDto ToUpdateDto(this CustomTemplateRequest request)
        {
            return new(request.Name, request.Category, request.Unit);
        }

        public static CreateReadingDto ToCreateDto(this ReadingRequest request)
        {
            return new(request.TemplateId, request.Value, request.Unit, request.RecordedAtUtc, request.Note);
        }

        public static UpdateReadingDto ToUpdateDto(this UpdateReadingRequest request)
        {
            return new(request.Value, request.Unit, request.RecordedAtUtc, request.Note);
        }

        public static TemplateResponse ToResponse(this TemplateDto dto)
        {
            return new(
                dto.Id,
                dto.Code,
                dto.Name,
                dto.Category,
                dto.NormalizedUnit,
                dto.AllowedUnits,
                dto.IsCustom,
                dto.IsTracked
            );
        }

        public static ReadingResponse ToResponse(this ReadingDto dto)
        {
            return new(
                dto.Id,
                dto.TemplateId,
                dto.TemplateName,
                dto.Value,
                dto.Unit,
                dto.RecordedAtUtc,
                dto.Note
            );
        }
    }
}
