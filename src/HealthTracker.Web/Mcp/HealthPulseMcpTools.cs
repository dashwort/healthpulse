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
        private const int MaxImportJsonCharacters = 524_288;
        private const int MaxImportedTemplates = 100;
        private const int MaxImportedReadings = 5_000;

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
            var import = ParseImport(json);
            return await tracker.ExecuteInTransactionAsync(async () =>
            {
                var templateIds = new Dictionary<Guid, Guid>();
                foreach (var template in import.Templates)
                {
                    if (!template.IsCustom)
                    {
                        continue;
                    }

                    var created = await tracker.CreateCustomTemplateAsync(
                        new CreateCustomTemplateDto(template.Name, template.Category, template.NormalizedUnit), ct);
                    templateIds[template.Id] = created.Id;
                }

                var catalogue = await tracker.GetCatalogueAsync(ct);
                var imported = 0;
                foreach (var reading in import.Readings)
                {
                    var templateId = templateIds.GetValueOrDefault(reading.TemplateId, reading.TemplateId);
                    var template = catalogue.SingleOrDefault(x => x.Id == templateId)
                        ?? throw new InvalidOperationException("An imported reading references an unavailable template.");
                    if (!template.IsTracked)
                    {
                        await tracker.TrackTemplateAsync(templateId, ct);
                    }

                    await tracker.CreateReadingAsync(
                        new CreateReadingDto(templateId, reading.Value, reading.Unit, reading.RecordedAtUtc, reading.Note), ct);
                    imported++;
                }

                return $"Imported {templateIds.Count} custom templates and {imported} readings.";
            }, ct);
        }

        private static ImportDocument ParseImport(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxImportJsonCharacters)
            {
                throw new InvalidOperationException("Import JSON exceeds the supported size.");
            }

            try
            {
                var document = JsonSerializer.Deserialize<ImportDocument>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                if (document?.Templates is null || document.Readings is null)
                {
                    throw new InvalidOperationException("Import JSON must contain templates and readings arrays.");
                }
                if (
                    document.Templates.Count > MaxImportedTemplates
                    || document.Readings.Count > MaxImportedReadings
                )
                {
                    throw new InvalidOperationException("Import JSON exceeds the supported record limit.");
                }

                foreach (var template in document.Templates)
                {
                    if (template.Id == Guid.Empty || string.IsNullOrWhiteSpace(template.Name) || string.IsNullOrWhiteSpace(template.Category) || string.IsNullOrWhiteSpace(template.NormalizedUnit))
                    {
                        throw new InvalidOperationException("Import JSON contains an invalid template.");
                    }
                }

                foreach (var reading in document.Readings)
                {
                    if (reading.TemplateId == Guid.Empty || string.IsNullOrWhiteSpace(reading.Unit))
                    {
                        throw new InvalidOperationException("Import JSON contains an invalid reading.");
                    }
                }

                return document;
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("Import JSON is invalid.", exception);
            }
        }

        private sealed record ImportDocument(IReadOnlyCollection<ImportedTemplate> Templates, IReadOnlyCollection<ImportedReading> Readings);
        private sealed record ImportedTemplate(Guid Id, string Name, string Category, string NormalizedUnit, bool IsCustom);
        private sealed record ImportedReading(Guid TemplateId, decimal Value, string Unit, DateTimeOffset RecordedAtUtc, string? Note);

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
