using HealthTracker.Infrastructure.Persistence.Models;

using Microsoft.EntityFrameworkCore;

namespace HealthTracker.Infrastructure.Persistence
{
    public sealed class HealthTrackerDbContext(DbContextOptions<HealthTrackerDbContext> options)
        : DbContext(options)
    {
        public DbSet<UserRecord> Users => Set<UserRecord>();
        public DbSet<AllowedUserRecord> AllowedUsers => Set<AllowedUserRecord>();
        public DbSet<PersonalAccessTokenRecord> PersonalAccessTokens => Set<PersonalAccessTokenRecord>();
        public DbSet<McpAuditLogRecord> McpAuditLogs => Set<McpAuditLogRecord>();
        public DbSet<TemplateRecord> Templates => Set<TemplateRecord>();
        public DbSet<TrackedTemplateRecord> TrackedTemplates => Set<TrackedTemplateRecord>();
        public DbSet<ReadingRecord> Readings => Set<ReadingRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserRecord>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Subject).HasMaxLength(255).IsRequired();
                entity.Property(x => x.DisplayName).HasMaxLength(255).IsRequired();
                entity.HasIndex(x => x.Subject).IsUnique();

                entity.Property(x => x.CreatedUtc)
                    .HasConversion(
                        v => v.UtcTicks,
                        v => new DateTimeOffset(v, TimeSpan.Zero)
                    )
                    .HasColumnType("INTEGER");
            });
            modelBuilder.Entity<AllowedUserRecord>(entity =>
            {
                entity.ToTable("AllowedUsers");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
                entity.Property(x => x.NormalizedEmail).HasMaxLength(320).IsRequired();
                entity.Property(x => x.Role).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => x.NormalizedEmail).IsUnique();
                entity.HasIndex(x => new { x.Role, x.DeletedUtc });

                entity.Property(x => x.CreatedUtc).HasConversion(
                    v => v.UtcTicks,
                    v => new DateTimeOffset(v, TimeSpan.Zero)
                ).HasColumnType("INTEGER");
                entity.Property(x => x.FirstSignedInUtc).HasConversion(
                    v => v.HasValue ? v.Value.UtcTicks : (long?)null,
                    v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null
                ).HasColumnType("INTEGER");
                entity.Property(x => x.LastSignedInUtc).HasConversion(
                    v => v.HasValue ? v.Value.UtcTicks : (long?)null,
                    v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null
                ).HasColumnType("INTEGER");
                entity.Property(x => x.DeletedUtc).HasConversion(
                    v => v.HasValue ? v.Value.UtcTicks : (long?)null,
                    v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null
                ).HasColumnType("INTEGER");
            });
            modelBuilder.Entity<PersonalAccessTokenRecord>(entity =>
            {
                entity.ToTable("PersonalAccessTokens");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Prefix).HasMaxLength(16).IsRequired();
                entity.Property(x => x.Hash).HasMaxLength(64).IsRequired();
                entity.HasIndex(x => x.Hash).IsUnique();
                entity.HasIndex(x => new { x.AllowedUserId, x.RevokedUtc });
                ConfigureUtc(entity.Property(x => x.CreatedUtc));
                ConfigureUtc(entity.Property(x => x.ExpiresUtc));
                ConfigureNullableUtc(entity.Property(x => x.LastUsedUtc));
                ConfigureNullableUtc(entity.Property(x => x.RevokedUtc));
            });
            modelBuilder.Entity<McpAuditLogRecord>(entity =>
            {
                entity.ToTable("McpAuditLogs");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Method).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
                entity.HasIndex(x => new { x.PersonalAccessTokenId, x.OccurredUtc });
                entity.HasIndex(x => new { x.AllowedUserId, x.OccurredUtc });
                ConfigureUtc(entity.Property(x => x.OccurredUtc));
            });
            modelBuilder.Entity<TemplateRecord>(entity =>
            {
                entity.ToTable("Templates");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Code).HasMaxLength(100);
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
                entity.Property(x => x.UnitCategory).HasMaxLength(50).IsRequired();
                entity.Property(x => x.NormalizedUnit).HasMaxLength(30).IsRequired();
                entity.Property(x => x.AllowedUnits).HasMaxLength(500).IsRequired();
                entity.HasIndex(x => x.Code).IsUnique();
                entity.HasIndex(x => new { x.OwnerUserId, x.DeletedUtc });

                entity.Property(x => x.CreatedUtc)
                    .HasConversion(
                        v => v.UtcTicks,
                        v => new DateTimeOffset(v, TimeSpan.Zero)
                    )
                    .HasColumnType("INTEGER");

                entity.Property(x => x.DeletedUtc)
                    .HasConversion(
                        v => v.HasValue ? v.Value.UtcTicks : (long?)null,
                        v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null
                    )
                    .HasColumnType("INTEGER");
            });
            modelBuilder.Entity<TrackedTemplateRecord>(entity =>
            {
                entity.ToTable("TrackedTemplates");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.UserId, x.TemplateId }).IsUnique();
                entity
                    .HasOne(x => x.Template)
                    .WithMany()
                    .HasForeignKey(x => x.TemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.Property(x => x.CreatedUtc)
                    .HasConversion(
                        v => v.UtcTicks,
                        v => new DateTimeOffset(v, TimeSpan.Zero)
                    )
                    .HasColumnType("INTEGER");

                entity.Property(x => x.DeletedUtc)
                    .HasConversion(
                        v => v.HasValue ? v.Value.UtcTicks : (long?)null,
                        v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null
                    )
                    .HasColumnType("INTEGER");
            });
            modelBuilder.Entity<ReadingRecord>(entity =>
            {
                entity.ToTable("Readings");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Value).HasPrecision(18, 6);
                entity.Property(x => x.Unit).HasMaxLength(30).IsRequired();
                entity.Property(x => x.Note).HasMaxLength(140);

                // Persist RecordedAtUtc as integer ticks so SQLite can perform ORDER BY and comparisons
                entity.Property(x => x.RecordedAtUtc)
                    .HasConversion(
                        v => v.UtcTicks,
                        v => new DateTimeOffset(v, TimeSpan.Zero)
                    )
                    .HasColumnType("INTEGER");

                // Also convert CreatedUtc/DeletedUtc for consistency
                entity.Property(x => x.CreatedUtc)
                    .HasConversion(
                        v => v.UtcTicks,
                        v => new DateTimeOffset(v, TimeSpan.Zero)
                    )
                    .HasColumnType("INTEGER");

                entity.Property(x => x.DeletedUtc)
                    .HasConversion(
                        v => v.HasValue ? v.Value.UtcTicks : (long?)null,
                        v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null
                    )
                    .HasColumnType("INTEGER");

                entity.HasIndex(x => new
                {
                    x.UserId,
                    x.TemplateId,
                    x.RecordedAtUtc,
                });

                entity
                    .HasOne(x => x.Template)
                    .WithMany()
                    .HasForeignKey(x => x.TemplateId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureUtc(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTimeOffset> property)
        {
            property.HasConversion(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)).HasColumnType("INTEGER");
        }

        private static void ConfigureNullableUtc(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTimeOffset?> property)
        {
            property.HasConversion(v => v.HasValue ? v.Value.UtcTicks : (long?)null, v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : (DateTimeOffset?)null).HasColumnType("INTEGER");
        }
    }
}
