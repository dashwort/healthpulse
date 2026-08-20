using HealthTracker.Domain.Models;

namespace HealthTracker.Application.Abstractions
{
    public interface IHealthDataStore
    {
        Task<ApplicationUser?> FindUserBySubjectAsync(
            string subject,
            CancellationToken cancellationToken
        );
        Task AddUserAsync(ApplicationUser user, CancellationToken cancellationToken);
        Task<IReadOnlyCollection<MeasurementTemplate>> GetCatalogueAsync(
            CancellationToken cancellationToken
        );
        Task<MeasurementTemplate?> GetTemplateForUserAsync(
            Guid userId,
            Guid templateId,
            bool includeDeleted,
            CancellationToken cancellationToken
        );
        Task<IReadOnlyCollection<UserTrackedTemplate>> GetTrackedTemplatesAsync(
            Guid userId,
            CancellationToken cancellationToken
        );
        Task<UserTrackedTemplate?> GetTrackingAsync(
            Guid userId,
            Guid templateId,
            bool includeDeleted,
            CancellationToken cancellationToken
        );
        Task AddTrackingAsync(UserTrackedTemplate tracking, CancellationToken cancellationToken);
        Task UpdateTrackingAsync(UserTrackedTemplate tracking, CancellationToken cancellationToken);
        Task AddTemplateAsync(MeasurementTemplate template, CancellationToken cancellationToken);
        Task UpdateTemplateAsync(MeasurementTemplate template, CancellationToken cancellationToken);
        Task AddReadingAsync(HealthReading reading, CancellationToken cancellationToken);
        Task<HealthReading?> GetReadingAsync(
            Guid userId,
            Guid readingId,
            bool includeDeleted,
            CancellationToken cancellationToken
        );
        Task<IReadOnlyCollection<HealthReading>> GetReadingsAsync(
            Guid userId,
            Guid? templateId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            CancellationToken cancellationToken
        );
        Task UpdateReadingAsync(HealthReading reading, CancellationToken cancellationToken);
        Task<int> PurgeSoftDeletedAsync(DateTimeOffset beforeUtc, CancellationToken cancellationToken);
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
