using System.ComponentModel;
using System.Text.Json;

using HealthTracker.Application.Dtos;
using HealthTracker.Application.Services;

using ModelContextProtocol.Server;

namespace HealthTracker.Web.Mcp
{
    [McpServerToolType]
    public sealed class HealthPulseMcpTools(HealthTrackerService tracker, PersonalAccessTokenService tokens)
    {
        [McpServerTool, Description("Lists the current user's measurement templates and tracking state.")]
        public Task<IReadOnlyCollection<TemplateDto>> ListTemplates(CancellationToken ct) => tracker.GetCatalogueAsync(ct);

        [McpServerTool, Description("Creates a personal measurement template and starts tracking it.")]
        public Task<TemplateDto> CreateCustomTemplate(string name, string category, string unit, CancellationToken ct) => tracker.CreateCustomTemplateAsync(new CreateCustomTemplateDto(name, category, unit), ct);

        [McpServerTool, Description("Updates one of the current user's personal measurement templates.")]
        public Task<TemplateDto> UpdateCustomTemplate(Guid templateId, string name, string category, string unit, CancellationToken ct) => tracker.UpdateCustomTemplateAsync(templateId, new UpdateCustomTemplateDto(name, category, unit), ct);

        [McpServerTool, Description("Soft-deletes one of the current user's personal measurement templates.")]
        public Task DeleteCustomTemplate(Guid templateId, CancellationToken ct) => tracker.DeleteCustomTemplateAsync(templateId, ct);

        [McpServerTool, Description("Turns tracking on for one of the current user's available templates.")]
        public Task TrackTemplate(Guid templateId, CancellationToken ct) => tracker.TrackTemplateAsync(templateId, ct);

        [McpServerTool, Description("Turns tracking off for one of the current user's tracked templates. Historic readings remain available.")]
        public Task UntrackTemplate(Guid templateId, CancellationToken ct) => tracker.StopTrackingAsync(templateId, ct);

        [McpServerTool, Description("Lists the current user's readings, optionally filtered by template and UTC date range.")]
        public Task<IReadOnlyCollection<ReadingDto>> ListReadings(Guid? templateId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct) => tracker.GetReadingsAsync(templateId, fromUtc, toUtc, ct);

        [McpServerTool, Description("Adds a reading for a template the current user is tracking.")]
        public Task<ReadingDto> AddReading(Guid templateId, decimal value, string unit, DateTimeOffset recordedAtUtc, string? note, CancellationToken ct) => tracker.CreateReadingAsync(new CreateReadingDto(templateId, value, unit, recordedAtUtc, note), ct);

        [McpServerTool, Description("Updates a reading owned by the current user.")]
        public Task<ReadingDto> UpdateReading(Guid readingId, decimal value, string unit, DateTimeOffset recordedAtUtc, string? note, CancellationToken ct) => tracker.UpdateReadingAsync(readingId, new UpdateReadingDto(value, unit, recordedAtUtc, note), ct);

        [McpServerTool, Description("Soft-deletes a reading owned by the current user.")]
        public Task DeleteReading(Guid readingId, CancellationToken ct) => tracker.DeleteReadingAsync(readingId, ct);

        [McpServerTool, Description("Exports the current user's templates and readings as JSON.")]
        public async Task<string> ExportJson(CancellationToken ct) => JsonSerializer.Serialize(new { templates = await tracker.GetCatalogueAsync(ct), readings = await tracker.GetReadingsAsync(null, null, null, ct) });

        [McpServerTool, Description("Additively imports HealthPulse JSON previously produced by export_json. Existing records are never overwritten.")]
        public async Task<string> ImportJson(string json, CancellationToken ct)
        {
            using var document = JsonDocument.Parse(json);
            var map = new Dictionary<Guid, Guid>(); var templatesImported = 0; var readingsImported = 0;
            if (document.RootElement.TryGetProperty("templates", out var templates)) foreach (var source in templates.EnumerateArray())
            {
                if (!source.TryGetProperty("isCustom", out var custom) || !custom.GetBoolean()) continue;
                var created = await tracker.CreateCustomTemplateAsync(new CreateCustomTemplateDto(source.GetProperty("name").GetString() ?? string.Empty, source.GetProperty("category").GetString() ?? string.Empty, source.GetProperty("normalizedUnit").GetString() ?? string.Empty), ct);
                if (source.TryGetProperty("id", out var oldId) && oldId.TryGetGuid(out var parsed)) map[parsed] = created.Id;
                templatesImported++;
            }
            if (document.RootElement.TryGetProperty("readings", out var readings)) foreach (var source in readings.EnumerateArray())
            {
                var sourceId = source.GetProperty("templateId").GetGuid(); var templateId = map.TryGetValue(sourceId, out var mapped) ? mapped : sourceId;
                await tracker.CreateReadingAsync(new CreateReadingDto(templateId, source.GetProperty("value").GetDecimal(), source.GetProperty("unit").GetString() ?? string.Empty, source.GetProperty("recordedAtUtc").GetDateTimeOffset(), source.TryGetProperty("note", out var note) && note.ValueKind != JsonValueKind.Null ? note.GetString() : null), ct); readingsImported++;
            }
            return $"Imported {templatesImported} custom templates and {readingsImported} readings.";
        }

        [McpServerTool, Description("Administrators only: lists approved users.")]
        public Task<IReadOnlyCollection<AllowedUserDto>> ListUsers(bool includeArchived, CancellationToken ct) => tracker.GetAllowedUsersAsync(includeArchived, ct);
        [McpServerTool, Description("Administrators only: approves or reactivates a user.")]
        public Task<AllowedUserDto> AddUser(string email, string role, CancellationToken ct) => tracker.AddAllowedUserAsync(new AddAllowedUserDto(email, role), ct);
        [McpServerTool, Description("Administrators only: changes a user's role.")]
        public Task<AllowedUserDto> UpdateUserRole(Guid allowedUserId, string role, CancellationToken ct) => tracker.UpdateAllowedUserRoleAsync(allowedUserId, new UpdateAllowedUserRoleDto(role), ct);
        [McpServerTool, Description("Administrators only: disables a user and blocks their tokens.")]
        public Task ArchiveUser(Guid allowedUserId, CancellationToken ct) => tracker.ArchiveAllowedUserAsync(allowedUserId, ct);
        [McpServerTool, Description("Administrators only: revokes one of a user's personal access tokens.")]
        public Task RevokeUserToken(Guid allowedUserId, Guid tokenId, CancellationToken ct) => tokens.RevokeTokenAsync(tokenId, allowedUserId, ct);
    }
}
