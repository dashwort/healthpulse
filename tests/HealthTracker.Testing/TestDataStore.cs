using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;

namespace HealthTracker.Testing;

public sealed class TestDataStore : IHealthDataStore
{
    public TestDataStore(
        string subject = "test-user",
        string email = "test@example.com",
        AllowedUserRole role = AllowedUserRole.Admin
    )
    {
        CurrentUser = new ApplicationUser { Subject = subject, DisplayName = "Test user" };
        CurrentAllowedUser = new AllowedUser
        {
            Email = email,
            NormalizedEmail = email.Trim().ToUpperInvariant(),
            Role = role,
            ApplicationUserId = CurrentUser.Id,
        };
        AllowedUsers.Add(CurrentAllowedUser);
    }

    public ApplicationUser CurrentUser { get; }

    public AllowedUser CurrentAllowedUser { get; }

    public List<ApplicationUser> Users { get; } = [];

    public List<AllowedUser> AllowedUsers { get; } = [];

    public List<MeasurementTemplate> Templates { get; } = [];

    public List<UserTrackedTemplate> Trackings { get; } = [];

    public List<HealthReading> Readings { get; } = [];

    public List<PersonalAccessToken> Tokens { get; } = [];

    public List<McpAuditLog> AuditLogs { get; } = [];

    public List<AccessActivity> AccessActivities { get; } = [];

    public List<MobileAuthorizationRequest> MobileAuthorizationRequests { get; } = [];

    public List<MobileSession> MobileSessions { get; } = [];

    public int SaveChangesCount { get; private set; }

    public int TransactionCount { get; private set; }

    public Task<ApplicationUser?> FindUserBySubjectAsync(string subject, CancellationToken ct) =>
        Task.FromResult<ApplicationUser?>(
            CurrentUser.Subject == subject
                ? CurrentUser
                : Users.SingleOrDefault(user => user.Subject == subject)
        );

    public Task AddUserAsync(ApplicationUser user, CancellationToken ct)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task<AllowedUser?> FindAllowedUserByEmailAsync(
        string normalizedEmail,
        bool includeDeleted,
        CancellationToken ct
    ) => Task.FromResult(
        AllowedUsers.SingleOrDefault(
            user =>
                user.NormalizedEmail == normalizedEmail
                && (includeDeleted || user.DeletedUtc is null)
        )
    );

    public Task<AllowedUser?> FindAllowedUserByIdAsync(
        Guid allowedUserId,
        bool includeDeleted,
        CancellationToken ct
    ) => Task.FromResult(
        AllowedUsers.SingleOrDefault(
            user =>
                user.Id == allowedUserId && (includeDeleted || user.DeletedUtc is null)
        )
    );

    public Task<ApplicationUser?> FindUserByIdAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult<ApplicationUser?>(
            CurrentUser.Id == userId
                ? CurrentUser
                : Users.SingleOrDefault(user => user.Id == userId)
        );

    public Task<IReadOnlyCollection<AllowedUser>> GetAllowedUsersAsync(
        bool includeDeleted,
        CancellationToken ct
    ) => Task.FromResult<IReadOnlyCollection<AllowedUser>>(
        [.. AllowedUsers.Where(user => includeDeleted || user.DeletedUtc is null)]
    );

    public Task<int> CountActiveAdministratorsAsync(CancellationToken ct) =>
        Task.FromResult(
            AllowedUsers.Count(
                user => user.Role == AllowedUserRole.Admin && user.DeletedUtc is null
            )
        );

    public Task AddAllowedUserAsync(AllowedUser user, CancellationToken ct)
    {
        AllowedUsers.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAllowedUserAsync(AllowedUser user, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<int> CountActiveTokensAsync(Guid allowedUserId, CancellationToken ct) =>
        Task.FromResult(
            Tokens.Count(
                token =>
                    token.AllowedUserId == allowedUserId
                    && token.RevokedUtc is null
                    && token.ExpiresUtc > DateTimeOffset.UtcNow
            )
        );

    public Task<PersonalAccessToken?> FindActiveTokenByHashAsync(
        string hash,
        CancellationToken ct
    ) => Task.FromResult(
        Tokens.SingleOrDefault(
            token =>
                token.Hash == hash
                && token.RevokedUtc is null
                && token.ExpiresUtc > DateTimeOffset.UtcNow
        )
    );

    public Task<IReadOnlyCollection<PersonalAccessToken>> GetTokensAsync(
        Guid allowedUserId,
        CancellationToken ct
    ) => Task.FromResult<IReadOnlyCollection<PersonalAccessToken>>(
        [.. Tokens.Where(token => token.AllowedUserId == allowedUserId)]
    );

    public Task AddTokenAsync(PersonalAccessToken token, CancellationToken ct)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task UpdateTokenAsync(PersonalAccessToken token, CancellationToken ct) =>
        Task.CompletedTask;

    public Task AddMcpAuditLogAsync(McpAuditLog auditLog, CancellationToken ct)
    {
        AuditLogs.Add(auditLog);
        return Task.CompletedTask;
    }

    public Task UpdateMcpAuditLogAsync(McpAuditLog auditLog, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<int> CountMcpCallsSinceAsync(
        Guid tokenId,
        DateTimeOffset sinceUtc,
        CancellationToken ct
    ) => Task.FromResult(
        AuditLogs.Count(
            auditLog =>
                auditLog.PersonalAccessTokenId == tokenId
                && auditLog.OccurredUtc >= sinceUtc
        )
    );

    public Task<int> PurgeMcpAuditLogsAsync(DateTimeOffset beforeUtc, CancellationToken ct)
    {
        var oldLogs = AuditLogs.Where(log => log.OccurredUtc < beforeUtc).ToArray();
        foreach (var oldLog in oldLogs)
        {
            AuditLogs.Remove(oldLog);
        }

        return Task.FromResult(oldLogs.Length);
    }

    public Task AddAccessActivityAsync(AccessActivity activity, CancellationToken ct)
    {
        AccessActivities.Add(activity);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyCollection<AccessActivity> Items, int TotalCount)> GetAccessActivitiesPageAsync(
        Guid? allowedUserId,
        AccessActivityType? type,
        AccessActivityOutcome? outcome,
        int page,
        int pageSize,
        CancellationToken ct
    )
    {
        var activities = AccessActivities
            .Where(activity =>
                (!allowedUserId.HasValue || activity.AllowedUserId == allowedUserId)
                && (!type.HasValue || activity.Type == type)
                && (!outcome.HasValue || activity.Outcome == outcome)
            )
            .OrderByDescending(activity => activity.OccurredUtc)
            .ToArray();
        var skip = (long)(page - 1) * pageSize;
        var items = skip >= activities.Length
            ? Array.Empty<AccessActivity>()
            : activities.Skip((int)skip).Take(pageSize).ToArray();
        return Task.FromResult<(IReadOnlyCollection<AccessActivity>, int)>((items, activities.Length));
    }

    public Task<int> PurgeAccessActivitiesAsync(DateTimeOffset beforeUtc, CancellationToken ct)
    {
        var oldActivities = AccessActivities.Where(activity => activity.OccurredUtc < beforeUtc).ToArray();
        foreach (var activity in oldActivities)
        {
            AccessActivities.Remove(activity);
        }

        return Task.FromResult(oldActivities.Length);
    }

    public Task AddMobileAuthorizationRequestAsync(
        MobileAuthorizationRequest request,
        CancellationToken ct
    )
    {
        MobileAuthorizationRequests.Add(request);
        return Task.CompletedTask;
    }

    public Task<MobileAuthorizationRequest?> GetMobileAuthorizationRequestAsync(
        Guid requestId,
        CancellationToken ct
    ) => Task.FromResult<MobileAuthorizationRequest?>(
        MobileAuthorizationRequests.SingleOrDefault(request => request.Id == requestId)
    );

    public Task<MobileAuthorizationRequest?> FindMobileAuthorizationRequestByCodeHashAsync(
        string authorizationCodeHash,
        CancellationToken ct
    ) => Task.FromResult<MobileAuthorizationRequest?>(
        MobileAuthorizationRequests.SingleOrDefault(
            request => request.AuthorizationCodeHash == authorizationCodeHash
        )
    );

    public Task UpdateMobileAuthorizationRequestAsync(
        MobileAuthorizationRequest request,
        CancellationToken ct
    ) => Task.CompletedTask;

    public Task AddMobileSessionAsync(MobileSession session, CancellationToken ct)
    {
        MobileSessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<MobileSession?> FindActiveMobileSessionByAccessHashAsync(
        string accessTokenHash,
        CancellationToken ct
    ) => Task.FromResult<MobileSession?>(
        MobileSessions.SingleOrDefault(
            session => session.AccessTokenHash == accessTokenHash && session.RevokedUtc is null
        )
    );

    public Task<MobileSession?> FindActiveMobileSessionByRefreshHashAsync(
        string refreshTokenHash,
        CancellationToken ct
    ) => Task.FromResult<MobileSession?>(
        MobileSessions.SingleOrDefault(
            session => session.RefreshTokenHash == refreshTokenHash && session.RevokedUtc is null
        )
    );

    public Task UpdateMobileSessionAsync(MobileSession session, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<IReadOnlyCollection<MeasurementTemplate>> GetCatalogueAsync(
        Guid userId,
        CancellationToken ct
    ) => Task.FromResult<IReadOnlyCollection<MeasurementTemplate>>(
        [
            .. Templates.Where(
                template =>
                    template.DeletedUtc is null
                    && (template.OwnerUserId is null || template.OwnerUserId == userId)
            ),
        ]
    );

    public Task<MeasurementTemplate?> GetTemplateForUserAsync(
        Guid userId,
        Guid templateId,
        bool includeDeleted,
        CancellationToken ct
    ) => Task.FromResult(
        Templates.SingleOrDefault(
            template =>
                template.Id == templateId
                && (template.OwnerUserId is null || template.OwnerUserId == userId)
                && (includeDeleted || template.DeletedUtc is null)
        )
    );

    public Task<IReadOnlyCollection<UserTrackedTemplate>> GetTrackedTemplatesAsync(
        Guid userId,
        CancellationToken ct
    ) => Task.FromResult<IReadOnlyCollection<UserTrackedTemplate>>(
        [
            .. Trackings.Where(
                tracking =>
                    tracking.UserId == userId
                    && tracking.DeletedUtc is null
                    && tracking.Template.DeletedUtc is null
            ),
        ]
    );

    public Task<UserTrackedTemplate?> GetTrackingAsync(
        Guid userId,
        Guid templateId,
        bool includeDeleted,
        CancellationToken ct
    ) => Task.FromResult(
        Trackings.SingleOrDefault(
            tracking =>
                tracking.UserId == userId
                && tracking.TemplateId == templateId
                && (includeDeleted || tracking.DeletedUtc is null)
                && tracking.Template.DeletedUtc is null
        )
    );

    public Task AddTrackingAsync(UserTrackedTemplate tracking, CancellationToken ct)
    {
        Trackings.Add(tracking);
        return Task.CompletedTask;
    }

    public Task UpdateTrackingAsync(UserTrackedTemplate tracking, CancellationToken ct) =>
        Task.CompletedTask;

    public Task AddTemplateAsync(MeasurementTemplate template, CancellationToken ct)
    {
        Templates.Add(template);
        return Task.CompletedTask;
    }

    public Task UpdateTemplateAsync(MeasurementTemplate template, CancellationToken ct) =>
        Task.CompletedTask;

    public Task AddReadingAsync(HealthReading reading, CancellationToken ct)
    {
        Readings.Add(reading);
        return Task.CompletedTask;
    }

    public Task<HealthReading?> GetReadingAsync(
        Guid userId,
        Guid readingId,
        bool includeDeleted,
        CancellationToken ct
    ) => Task.FromResult(
        Readings.SingleOrDefault(
            reading =>
                reading.UserId == userId
                && reading.Id == readingId
                && (includeDeleted || reading.DeletedUtc is null)
        )
    );

    public Task<IReadOnlyCollection<HealthReading>> GetReadingsAsync(
        Guid userId,
        Guid? templateId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken ct
    ) => Task.FromResult<IReadOnlyCollection<HealthReading>>(
        [
            .. Readings
                .Where(
                    reading =>
                        reading.UserId == userId
                        && reading.DeletedUtc is null
                        && (!templateId.HasValue || reading.TemplateId == templateId)
                        && (!fromUtc.HasValue || reading.RecordedAtUtc >= fromUtc)
                        && (!toUtc.HasValue || reading.RecordedAtUtc <= toUtc)
                )
                .OrderByDescending(reading => reading.RecordedAtUtc),
        ]
    );

    public Task<(IReadOnlyCollection<HealthReading> Items, int TotalCount)> GetReadingsPageAsync(
        Guid userId,
        Guid? templateId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int page,
        int pageSize,
        CancellationToken ct
    )
    {
        var readings = Readings
            .Where(
                reading =>
                    reading.UserId == userId
                    && reading.DeletedUtc is null
                    && (!templateId.HasValue || reading.TemplateId == templateId)
                    && (!fromUtc.HasValue || reading.RecordedAtUtc >= fromUtc)
                    && (!toUtc.HasValue || reading.RecordedAtUtc <= toUtc)
            )
            .OrderByDescending(reading => reading.RecordedAtUtc)
            .ToArray();
        var skip = (long)(page - 1) * pageSize;
        var items = skip >= readings.Length
            ? []
            : readings.Skip((int)skip).Take(pageSize).ToArray();
        return Task.FromResult<(IReadOnlyCollection<HealthReading>, int)>((items, readings.Length));
    }

    public Task UpdateReadingAsync(HealthReading reading, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<int> PurgeSoftDeletedAsync(DateTimeOffset beforeUtc, CancellationToken ct)
    {
        var readings = Readings.Where(reading => reading.DeletedUtc < beforeUtc).ToArray();
        var trackings = Trackings.Where(tracking => tracking.DeletedUtc < beforeUtc).ToArray();
        var templates = Templates
            .Where(template => template.OwnerUserId is not null && template.DeletedUtc < beforeUtc)
            .ToArray();
        foreach (var item in readings) Readings.Remove(item);
        foreach (var item in trackings) Trackings.Remove(item);
        foreach (var item in templates) Templates.Remove(item);
        return Task.FromResult(readings.Length + trackings.Length + templates.Length);
    }

    public Task SaveChangesAsync(CancellationToken ct)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct
    )
    {
        TransactionCount++;
        return await operation();
    }
}
