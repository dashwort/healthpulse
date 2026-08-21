using HealthTracker.Application.Abstractions;
using HealthTracker.Domain.Models;
using HealthTracker.Infrastructure.Persistence.Mappings;
using HealthTracker.Infrastructure.Persistence.Models;

using Microsoft.EntityFrameworkCore;

namespace HealthTracker.Infrastructure.Persistence
{
    public sealed class EfHealthDataStore(HealthTrackerDbContext db) : IHealthDataStore
    {
        public async Task<ApplicationUser?> FindUserBySubjectAsync(
            string subject,
            CancellationToken ct
        )
        {
            return (await db.Users.SingleOrDefaultAsync(x => x.Subject == subject, ct))?.ToDomain();
        }

        public Task AddUserAsync(ApplicationUser user, CancellationToken ct)
        {
            return db.Users.AddAsync(user.ToRecord(), ct).AsTask();
        }

        public async Task<IReadOnlyCollection<MeasurementTemplate>> GetCatalogueAsync(
            Guid userId,
            CancellationToken ct
        )
        {
            return [
                .. (
                    await db
                        .Templates.AsNoTracking()
                        .Where(x =>
                            x.DeletedUtc == null
                            && (x.OwnerUserId == null || x.OwnerUserId == userId)
                        )
                        .OrderBy(x => x.Name)
                        .ToArrayAsync(ct)
                ).Select(x => x.ToDomain()),
            ];
        }

        public async Task<MeasurementTemplate?> GetTemplateForUserAsync(
            Guid userId,
            Guid templateId,
            bool includeDeleted,
            CancellationToken ct
        )
        {
            return (
                await db.Templates.SingleOrDefaultAsync(
                    x =>
                        x.Id == templateId
                        && (x.OwnerUserId == null || x.OwnerUserId == userId)
                        && (includeDeleted || x.DeletedUtc == null),
                    ct
                )
            )?.ToDomain();
        }

        public async Task<IReadOnlyCollection<UserTrackedTemplate>> GetTrackedTemplatesAsync(
            Guid userId,
            CancellationToken ct
        )
        {
            return [
                .. (
                    await db
                        .TrackedTemplates.Include(x => x.Template)
                        .AsNoTracking()
                        .Where(x =>
                            x.UserId == userId && x.DeletedUtc == null && x.Template.DeletedUtc == null
                        )
                        .ToArrayAsync(ct)
                ).Select(x => x.ToDomain()),
            ];
        }

        public async Task<UserTrackedTemplate?> GetTrackingAsync(
            Guid userId,
            Guid templateId,
            bool includeDeleted,
            CancellationToken ct
        )
        {
            return (
                await db
                    .TrackedTemplates.Include(x => x.Template)
                    .SingleOrDefaultAsync(
                        x =>
                            x.UserId == userId
                            && x.TemplateId == templateId
                            && (includeDeleted || x.DeletedUtc == null)
                            && x.Template.DeletedUtc == null,
                        ct
                    )
            )?.ToDomain();
        }

        public Task AddTrackingAsync(UserTrackedTemplate tracking, CancellationToken ct)
        {
            return db.TrackedTemplates.AddAsync(tracking.ToRecord(), ct).AsTask();
        }

        public async Task UpdateTrackingAsync(UserTrackedTemplate tracking, CancellationToken ct)
        {
            var record = await db.TrackedTemplates.SingleAsync(
                x => x.Id == tracking.Id && x.UserId == tracking.UserId,
                ct
            );
            tracking.Apply(record);
        }

        public Task AddTemplateAsync(MeasurementTemplate template, CancellationToken ct)
        {
            return db.Templates.AddAsync(template.ToRecord(), ct).AsTask();
        }

        public async Task UpdateTemplateAsync(MeasurementTemplate template, CancellationToken ct)
        {
            var record = await db.Templates.SingleAsync(x => x.Id == template.Id, ct);
            template.Apply(record);
        }

        public Task AddReadingAsync(HealthReading reading, CancellationToken ct)
        {
            return db.Readings.AddAsync(reading.ToRecord(), ct).AsTask();
        }

        public async Task<HealthReading?> GetReadingAsync(
            Guid userId,
            Guid readingId,
            bool includeDeleted,
            CancellationToken ct
        )
        {
            return (
                await db
                    .Readings.Include(x => x.Template)
                    .SingleOrDefaultAsync(
                        x =>
                            x.Id == readingId
                            && x.UserId == userId
                            && (includeDeleted || x.DeletedUtc == null),
                        ct
                    )
            )?.ToDomain();
        }

        public async Task<IReadOnlyCollection<HealthReading>> GetReadingsAsync(
            Guid userId,
            Guid? templateId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            CancellationToken ct
        )
        {
            return [
                .. (
                    await db
                        .Readings.Include(x => x.Template)
                        .AsNoTracking()
                        .Where(x =>
                            x.UserId == userId
                            && x.DeletedUtc == null
                            && (!templateId.HasValue || x.TemplateId == templateId)
                            && (!fromUtc.HasValue || x.RecordedAtUtc >= fromUtc)
                            && (!toUtc.HasValue || x.RecordedAtUtc <= toUtc)
                        )
                        .OrderByDescending(x => x.RecordedAtUtc)
                        .ToArrayAsync(ct)
                ).Select(x => x.ToDomain()),
            ];
        }

        public async Task<ApplicationUser?> FindUserByIdAsync(Guid userId, CancellationToken ct)
        {
            return (await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct))?.ToDomain();
        }

        public async Task<AllowedUser?> FindAllowedUserByEmailAsync(
            string normalizedEmail,
            bool includeDeleted,
            CancellationToken ct
        )
        {
            return (await db.AllowedUsers.SingleOrDefaultAsync(
                x => x.NormalizedEmail == normalizedEmail && (includeDeleted || x.DeletedUtc == null),
                ct
            ))?.ToDomain();
        }

        public async Task<IReadOnlyCollection<AllowedUser>> GetAllowedUsersAsync(
            bool includeDeleted,
            CancellationToken ct
        )
        {
            var query = db.AllowedUsers.AsNoTracking();
            if (!includeDeleted)
            {
                query = query.Where(x => x.DeletedUtc == null);
            }

            return [.. (await query.OrderBy(x => x.Email).ToArrayAsync(ct)).Select(x => x.ToDomain())];
        }

        public async Task<AllowedUser?> FindAllowedUserByIdAsync(
            Guid allowedUserId,
            bool includeDeleted,
            CancellationToken ct
        )
        {
            return (await db.AllowedUsers.SingleOrDefaultAsync(
                x => x.Id == allowedUserId && (includeDeleted || x.DeletedUtc == null),
                ct
            ))?.ToDomain();
        }

        public Task<int> CountActiveAdministratorsAsync(CancellationToken ct)
        {
            return db.AllowedUsers.CountAsync(
                x => x.Role == AllowedUserRole.Admin.ToString() && x.DeletedUtc == null,
                ct
            );
        }

        public Task AddAllowedUserAsync(AllowedUser user, CancellationToken ct)
        {
            return db.AllowedUsers.AddAsync(user.ToRecord(), ct).AsTask();
        }

        public async Task UpdateAllowedUserAsync(AllowedUser user, CancellationToken ct)
        {
            var record = await db.AllowedUsers.SingleAsync(x => x.Id == user.Id, ct);
            user.Apply(record);
        }

        public async Task<int> CountActiveTokensAsync(Guid allowedUserId, CancellationToken ct)
        {
            var tokens = await db
                .PersonalAccessTokens.AsNoTracking()
                .Where(x => x.AllowedUserId == allowedUserId && x.RevokedUtc == null)
                .ToArrayAsync(ct);
            return tokens.Count(x => x.ExpiresUtc > DateTimeOffset.UtcNow);
        }

        public async Task<PersonalAccessToken?> FindActiveTokenByHashAsync(string hash, CancellationToken ct)
        {
            var token = await db.PersonalAccessTokens.SingleOrDefaultAsync(x => x.Hash == hash && x.RevokedUtc == null && x.ExpiresUtc > DateTimeOffset.UtcNow, ct);
            return token is null ? null : new PersonalAccessToken { Id = token.Id, AllowedUserId = token.AllowedUserId, Name = token.Name, Prefix = token.Prefix, Hash = token.Hash, CreatedUtc = token.CreatedUtc, ExpiresUtc = token.ExpiresUtc, LastUsedUtc = token.LastUsedUtc, RevokedUtc = token.RevokedUtc };
        }

        public async Task<IReadOnlyCollection<PersonalAccessToken>> GetTokensAsync(Guid allowedUserId, CancellationToken ct) =>
            [.. (await db.PersonalAccessTokens.AsNoTracking().Where(x => x.AllowedUserId == allowedUserId).OrderByDescending(x => x.CreatedUtc).ToArrayAsync(ct)).Select(x => new PersonalAccessToken { Id = x.Id, AllowedUserId = x.AllowedUserId, Name = x.Name, Prefix = x.Prefix, Hash = x.Hash, CreatedUtc = x.CreatedUtc, ExpiresUtc = x.ExpiresUtc, LastUsedUtc = x.LastUsedUtc, RevokedUtc = x.RevokedUtc })];

        public Task AddTokenAsync(PersonalAccessToken token, CancellationToken ct) => db.PersonalAccessTokens.AddAsync(new PersonalAccessTokenRecord { Id = token.Id, AllowedUserId = token.AllowedUserId, Name = token.Name, Prefix = token.Prefix, Hash = token.Hash, CreatedUtc = token.CreatedUtc, ExpiresUtc = token.ExpiresUtc, LastUsedUtc = token.LastUsedUtc, RevokedUtc = token.RevokedUtc }, ct).AsTask();

        public async Task UpdateTokenAsync(PersonalAccessToken token, CancellationToken ct)
        {
            var record = await db.PersonalAccessTokens.SingleAsync(x => x.Id == token.Id, ct);
            record.LastUsedUtc = token.LastUsedUtc;
            record.RevokedUtc = token.RevokedUtc;
        }

        public Task AddMcpAuditLogAsync(McpAuditLog auditLog, CancellationToken ct) => db.McpAuditLogs.AddAsync(new McpAuditLogRecord { Id = auditLog.Id, PersonalAccessTokenId = auditLog.PersonalAccessTokenId, AllowedUserId = auditLog.AllowedUserId, Method = auditLog.Method, Outcome = auditLog.Outcome, OccurredUtc = auditLog.OccurredUtc }, ct).AsTask();

        public Task<int> CountMcpCallsSinceAsync(Guid tokenId, DateTimeOffset sinceUtc, CancellationToken ct) => db.McpAuditLogs.CountAsync(x => x.PersonalAccessTokenId == tokenId && x.OccurredUtc >= sinceUtc, ct);

        public async Task<int> PurgeMcpAuditLogsAsync(DateTimeOffset beforeUtc, CancellationToken ct)
        {
            var logs = await db.McpAuditLogs.Where(x => x.OccurredUtc < beforeUtc).ToListAsync(ct);
            db.McpAuditLogs.RemoveRange(logs);
            await db.SaveChangesAsync(ct);
            return logs.Count;
        }

        public async Task<(IReadOnlyCollection<HealthReading> Items, int TotalCount)> GetReadingsPageAsync(
            Guid userId,
            Guid? templateId,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int page,
            int pageSize,
            CancellationToken ct
        )
        {
            var query = db
                .Readings.Include(x => x.Template)
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId
                    && x.DeletedUtc == null
                    && (!templateId.HasValue || x.TemplateId == templateId)
                    && (!fromUtc.HasValue || x.RecordedAtUtc >= fromUtc)
                    && (!toUtc.HasValue || x.RecordedAtUtc <= toUtc)
                );
            var totalCount = await query.CountAsync(ct);
            var skip = (long)(page - 1) * pageSize;
            if (skip >= totalCount)
            {
                return (Array.Empty<HealthReading>(), totalCount);
            }

            var items = await query
                .OrderByDescending(x => x.RecordedAtUtc)
                .Skip((int)skip)
                .Take(pageSize)
                .ToArrayAsync(ct);
            return ([.. items.Select(x => x.ToDomain())], totalCount);
        }

        public async Task UpdateReadingAsync(HealthReading reading, CancellationToken ct)
        {
            var record = await db.Readings.SingleAsync(
                x => x.Id == reading.Id && x.UserId == reading.UserId,
                ct
            );
            reading.Apply(record);
        }

        public async Task<int> PurgeSoftDeletedAsync(DateTimeOffset beforeUtc, CancellationToken ct)
        {
            // DateTimeOffset ordering is not translated by every EF provider (including SQLite).
            // Filtering the already-soft-deleted sets in memory keeps this adapter portable.
            var readings = (await db.Readings.Where(x => x.DeletedUtc != null).ToListAsync(ct))
                .Where(x => x.DeletedUtc < beforeUtc)
                .ToArray();
            var trackings = (await db.TrackedTemplates.Where(x => x.DeletedUtc != null).ToListAsync(ct))
                .Where(x => x.DeletedUtc < beforeUtc)
                .ToArray();
            var templates = (
                await db
                    .Templates.Where(x => x.OwnerUserId != null && x.DeletedUtc != null)
                    .ToListAsync(ct)
            )
                .Where(x => x.DeletedUtc < beforeUtc)
                .ToArray();
            db.Readings.RemoveRange(readings);
            db.TrackedTemplates.RemoveRange(trackings);
            db.Templates.RemoveRange(templates);
            await db.SaveChangesAsync(ct);
            return readings.Length + trackings.Length + templates.Length;
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            return db.SaveChangesAsync(ct);
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }
    }
}
