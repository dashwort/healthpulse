using HealthTracker.Domain.Models;
using HealthTracker.Infrastructure.Persistence.Mappings;

using Microsoft.EntityFrameworkCore;

namespace HealthTracker.Infrastructure.Persistence
{
    public sealed class DatabaseInitializer(HealthTrackerDbContext db)
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
        }
    }
}
