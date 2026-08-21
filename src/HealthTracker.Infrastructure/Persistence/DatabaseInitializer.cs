using HealthTracker.Domain.Models;
using HealthTracker.Infrastructure.Persistence.Mappings;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HealthTracker.Infrastructure.Persistence
{
    public sealed class DatabaseInitializer(HealthTrackerDbContext db, IConfiguration configuration)
    {
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await db.Database.MigrateAsync(cancellationToken);
            var knownIds = await db
                .Templates.Where(x => x.OwnerUserId == null)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var missing = BuiltInTemplates
                .All.Where(x => !knownIds.Contains(x.Id))
                .Select(x => x.ToRecord())
                .ToArray();
            if (missing.Length > 0)
            {
                await db.Templates.AddRangeAsync(missing, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            if (await db.AllowedUsers.AnyAsync(cancellationToken))
            {
                return;
            }

            var email = configuration["AccessControl:InitialAdministratorEmail"];
            if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 320)
            {
                throw new InvalidOperationException(
                    "AccessControl:InitialAdministratorEmail is required to initialize access control."
                );
            }

            var normalizedEmail = email.Trim().ToUpperInvariant();
            if (!System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _))
            {
                throw new InvalidOperationException(
                    "AccessControl:InitialAdministratorEmail must be a valid email address."
                );
            }

            await db.AllowedUsers.AddAsync(
                new Persistence.Models.AllowedUserRecord
                {
                    Id = Guid.NewGuid(),
                    Email = email.Trim(),
                    NormalizedEmail = normalizedEmail,
                    Role = AllowedUserRole.Admin.ToString(),
                    CreatedUtc = DateTimeOffset.UtcNow,
                },
                cancellationToken
            );
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
