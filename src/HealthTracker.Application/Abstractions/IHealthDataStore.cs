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
        Task<AllowedUser?> FindAllowedUserByEmailAsync(
            string normalizedEmail,
            bool includeDeleted,
            CancellationToken cancellationToken
        );
        Task<AllowedUser?> FindAllowedUserByIdAsync(
            Guid allowedUserId,
            bool includeDeleted,
            CancellationToken cancellationToken
        );
        Task<ApplicationUser?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);
        Task<IReadOnlyCollection<AllowedUser>> GetAllowedUsersAsync(
            bool includeDeleted,
            CancellationToken cancellationToken
        );
        Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken);
        Task AddAllowedUserAsync(AllowedUser user, CancellationToken cancellationToken);
        Task UpdateAllowedUserAsync(AllowedUser user, CancellationToken cancellationToken);
        Task<int> CountActiveTokensAsync(Guid allowedUserId, CancellationToken cancellationToken);
        Task<PersonalAccessToken?> FindActiveTokenByHashAsync(string hash, CancellationToken cancellationToken);
        Task<IReadOnlyCollection<PersonalAccessToken>> GetTokensAsync(Guid allowedUserId, CancellationToken cancellationToken);
        Task AddTokenAsync(PersonalAccessToken token, CancellationToken cancellationToken);
        Task UpdateTokenAsync(PersonalAccessToken token, CancellationToken cancellationToken);
        Task AddMcpAuditLogAsync(McpAuditLog auditLog, CancellationToken cancellationToken);
        Task UpdateMcpAuditLogAsync(McpAuditLog auditLog, CancellationToken cancellationToken);
        Task<int> CountMcpCallsSinceAsync(Guid tokenId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
        Task<int> PurgeMcpAuditLogsAsync(DateTimeOffset beforeUtc, CancellationToken cancellationToken);
        Task AddAccessActivityAsync(AccessActivity activity, CancellationToken cancellationToken);
        Task<(IReadOnlyCollection<AccessActivity> Items, int TotalCount)> GetAccessActivitiesPageAsync(
            Guid? allowedUserId,
            AccessActivityType? type,
            AccessActivityOutcome? outcome,
            int page,
            int pageSize,
            CancellationToken cancellationToken
        );
        Task<int> PurgeAccessActivitiesAsync(DateTimeOffset beforeUtc, CancellationToken cancellationToken);
        Task AddMobileAuthorizationRequestAsync(
            MobileAuthorizationRequest request,
            CancellationToken cancellationToken
        );
        Task<MobileAuthorizationRequest?> GetMobileAuthorizationRequestAsync(
            Guid requestId,
            CancellationToken cancellationToken
        );
        Task<MobileAuthorizationRequest?> FindMobileAuthorizationRequestByCodeHashAsync(
            string authorizationCodeHash,
            CancellationToken cancellationToken
        );
        Task UpdateMobileAuthorizationRequestAsync(
            MobileAuthorizationRequest request,
            CancellationToken cancellationToken
        );
        Task AddMobileSessionAsync(MobileSession session, CancellationToken cancellationToken);
        Task<MobileSession?> FindActiveMobileSessionByAccessHashAsync(
            string accessTokenHash,
            CancellationToken cancellationToken
        );
        Task<MobileSession?> FindActiveMobileSessionByRefreshHashAsync(
            string refreshTokenHash,
            CancellationToken cancellationToken
        );
        Task UpdateMobileSessionAsync(MobileSession session, CancellationToken cancellationToken);
        Task<IReadOnlyCollection<MeasurementTemplate>> GetCatalogueAsync(
            Guid userId,
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
        Task<(IReadOnlyCollection<HealthReading> Items, int TotalCount)> GetReadingsPageAsync(
            Guid userId,
            Guid? templateId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int page,
            int pageSize,
            CancellationToken cancellationToken
        );
        Task UpdateReadingAsync(HealthReading reading, CancellationToken cancellationToken);
        Task<int> PurgeSoftDeletedAsync(DateTimeOffset beforeUtc, CancellationToken cancellationToken);
        Task SaveChangesAsync(CancellationToken cancellationToken);
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);
    }
}
