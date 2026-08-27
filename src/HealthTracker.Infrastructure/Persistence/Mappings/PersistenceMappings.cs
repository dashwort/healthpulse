using HealthTracker.Domain.Models;
using HealthTracker.Infrastructure.Persistence.Models;

namespace HealthTracker.Infrastructure.Persistence.Mappings
{
    public static class PersistenceMappings
    {
        public static AllowedUser ToDomain(this AllowedUserRecord record)
        {
            return new()
            {
                Id = record.Id,
                Email = record.Email,
                NormalizedEmail = record.NormalizedEmail,
                Role = Enum.Parse<AllowedUserRole>(record.Role),
                ApplicationUserId = record.ApplicationUserId,
                CreatedUtc = record.CreatedUtc,
                FirstSignedInUtc = record.FirstSignedInUtc,
                LastSignedInUtc = record.LastSignedInUtc,
                DeletedUtc = record.DeletedUtc,
            };
        }

        public static AllowedUserRecord ToRecord(this AllowedUser user)
        {
            return new()
            {
                Id = user.Id,
                Email = user.Email,
                NormalizedEmail = user.NormalizedEmail,
                Role = user.Role.ToString(),
                ApplicationUserId = user.ApplicationUserId,
                CreatedUtc = user.CreatedUtc,
                FirstSignedInUtc = user.FirstSignedInUtc,
                LastSignedInUtc = user.LastSignedInUtc,
                DeletedUtc = user.DeletedUtc,
            };
        }

        public static void Apply(this AllowedUser source, AllowedUserRecord target)
        {
            target.Email = source.Email;
            target.Role = source.Role.ToString();
            target.ApplicationUserId = source.ApplicationUserId;
            target.FirstSignedInUtc = source.FirstSignedInUtc;
            target.LastSignedInUtc = source.LastSignedInUtc;
            target.DeletedUtc = source.DeletedUtc;
        }

        public static ApplicationUser ToDomain(this UserRecord record)
        {
            return new()
            {
                Id = record.Id,
                Subject = record.Subject,
                DisplayName = record.DisplayName,
                CreatedUtc = record.CreatedUtc,
            };
        }

        public static UserRecord ToRecord(this ApplicationUser user)
        {
            return new()
            {
                Id = user.Id,
                Subject = user.Subject,
                DisplayName = user.DisplayName,
                CreatedUtc = user.CreatedUtc,
            };
        }

        public static MeasurementTemplate ToDomain(this TemplateRecord record)
        {
            return new()
            {
                Id = record.Id,
                OwnerUserId = record.OwnerUserId,
                Code = record.Code,
                Name = record.Name,
                Category = record.Category,
                UnitCategory = record.UnitCategory,
                NormalizedUnit = record.NormalizedUnit,
                AllowedUnits = record.AllowedUnits.Split(
                    '|',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                ),
                CreatedUtc = record.CreatedUtc,
                DeletedUtc = record.DeletedUtc,
            };
        }

        public static TemplateRecord ToRecord(this MeasurementTemplate template)
        {
            return new()
            {
                Id = template.Id,
                OwnerUserId = template.OwnerUserId,
                Code = template.Code,
                Name = template.Name,
                Category = template.Category,
                UnitCategory = template.UnitCategory,
                NormalizedUnit = template.NormalizedUnit,
                AllowedUnits = string.Join('|', template.AllowedUnits),
                CreatedUtc = template.CreatedUtc,
                DeletedUtc = template.DeletedUtc,
            };
        }

        public static void Apply(this MeasurementTemplate source, TemplateRecord target)
        {
            target.Name = source.Name;
            target.Category = source.Category;
            target.UnitCategory = source.UnitCategory;
            target.NormalizedUnit = source.NormalizedUnit;
            target.AllowedUnits = string.Join('|', source.AllowedUnits);
            target.DeletedUtc = source.DeletedUtc;
        }

        public static UserTrackedTemplate ToDomain(this TrackedTemplateRecord record)
        {
            return new()
            {
                Id = record.Id,
                UserId = record.UserId,
                TemplateId = record.TemplateId,
                Template = record.Template.ToDomain(),
                CreatedUtc = record.CreatedUtc,
                DeletedUtc = record.DeletedUtc,
            };
        }

        public static TrackedTemplateRecord ToRecord(this UserTrackedTemplate tracking)
        {
            return new()
            {
                Id = tracking.Id,
                UserId = tracking.UserId,
                TemplateId = tracking.TemplateId,
                CreatedUtc = tracking.CreatedUtc,
                DeletedUtc = tracking.DeletedUtc,
            };
        }

        public static void Apply(this UserTrackedTemplate source, TrackedTemplateRecord target)
        {
            target.DeletedUtc = source.DeletedUtc;
        }

        public static HealthReading ToDomain(this ReadingRecord record)
        {
            return new()
            {
                Id = record.Id,
                UserId = record.UserId,
                TemplateId = record.TemplateId,
                TemplateName = record.Template.Name,
                Value = record.Value,
                Unit = record.Unit,
                Note = record.Note,
                RecordedAtUtc = record.RecordedAtUtc,
                CreatedUtc = record.CreatedUtc,
                DeletedUtc = record.DeletedUtc,
            };
        }

        public static ReadingRecord ToRecord(this HealthReading reading)
        {
            return new()
            {
                Id = reading.Id,
                UserId = reading.UserId,
                TemplateId = reading.TemplateId,
                Value = reading.Value,
                Unit = reading.Unit,
                Note = reading.Note,
                RecordedAtUtc = reading.RecordedAtUtc,
                CreatedUtc = reading.CreatedUtc,
                DeletedUtc = reading.DeletedUtc,
            };
        }

        public static void Apply(this HealthReading source, ReadingRecord target)
        {
            target.Value = source.Value;
            target.Unit = source.Unit;
            target.Note = source.Note;
            target.RecordedAtUtc = source.RecordedAtUtc;
            target.DeletedUtc = source.DeletedUtc;
        }

        public static AccessActivity ToDomain(this AccessActivityRecord record)
        {
            return new()
            {
                Id = record.Id,
                AllowedUserId = record.AllowedUserId,
                Type = Enum.Parse<AccessActivityType>(record.Type),
                Outcome = Enum.Parse<AccessActivityOutcome>(record.Outcome),
                FailureReason = string.IsNullOrEmpty(record.FailureReason)
                    ? null
                    : Enum.Parse<AccessActivityFailureReason>(record.FailureReason),
                OccurredUtc = record.OccurredUtc,
                SourceIpAddress = record.SourceIpAddress,
                UserAgent = record.UserAgent,
            };
        }

        public static AccessActivityRecord ToRecord(this AccessActivity activity)
        {
            return new()
            {
                Id = activity.Id,
                AllowedUserId = activity.AllowedUserId,
                Type = activity.Type.ToString(),
                Outcome = activity.Outcome.ToString(),
                FailureReason = activity.FailureReason?.ToString(),
                OccurredUtc = activity.OccurredUtc,
                SourceIpAddress = activity.SourceIpAddress,
                UserAgent = activity.UserAgent,
            };
        }

        public static MobileAuthorizationRequest ToDomain(
            this MobileAuthorizationRequestRecord record
        )
        {
            return new()
            {
                Id = record.Id,
                CodeChallenge = record.CodeChallenge,
                State = record.State,
                RedirectUri = record.RedirectUri,
                CreatedUtc = record.CreatedUtc,
                ExpiresUtc = record.ExpiresUtc,
                ApplicationUserId = record.ApplicationUserId,
                AuthorizationCodeHash = record.AuthorizationCodeHash,
                AuthorizationCodeExpiresUtc = record.AuthorizationCodeExpiresUtc,
                ConsumedUtc = record.ConsumedUtc,
            };
        }

        public static MobileAuthorizationRequestRecord ToRecord(this MobileAuthorizationRequest request)
        {
            return new()
            {
                Id = request.Id,
                CodeChallenge = request.CodeChallenge,
                State = request.State,
                RedirectUri = request.RedirectUri,
                CreatedUtc = request.CreatedUtc,
                ExpiresUtc = request.ExpiresUtc,
                ApplicationUserId = request.ApplicationUserId,
                AuthorizationCodeHash = request.AuthorizationCodeHash,
                AuthorizationCodeExpiresUtc = request.AuthorizationCodeExpiresUtc,
                ConsumedUtc = request.ConsumedUtc,
            };
        }

        public static void Apply(this MobileAuthorizationRequest source, MobileAuthorizationRequestRecord target)
        {
            target.ApplicationUserId = source.ApplicationUserId;
            target.AuthorizationCodeHash = source.AuthorizationCodeHash;
            target.AuthorizationCodeExpiresUtc = source.AuthorizationCodeExpiresUtc;
            target.ConsumedUtc = source.ConsumedUtc;
        }

        public static MobileSession ToDomain(this MobileSessionRecord record)
        {
            return new()
            {
                Id = record.Id,
                ApplicationUserId = record.ApplicationUserId,
                AccessTokenHash = record.AccessTokenHash,
                AccessTokenExpiresUtc = record.AccessTokenExpiresUtc,
                RefreshTokenHash = record.RefreshTokenHash,
                RefreshTokenExpiresUtc = record.RefreshTokenExpiresUtc,
                CreatedUtc = record.CreatedUtc,
                LastUsedUtc = record.LastUsedUtc,
                RevokedUtc = record.RevokedUtc,
            };
        }

        public static MobileSessionRecord ToRecord(this MobileSession session)
        {
            return new()
            {
                Id = session.Id,
                ApplicationUserId = session.ApplicationUserId,
                AccessTokenHash = session.AccessTokenHash,
                AccessTokenExpiresUtc = session.AccessTokenExpiresUtc,
                RefreshTokenHash = session.RefreshTokenHash,
                RefreshTokenExpiresUtc = session.RefreshTokenExpiresUtc,
                CreatedUtc = session.CreatedUtc,
                LastUsedUtc = session.LastUsedUtc,
                RevokedUtc = session.RevokedUtc,
            };
        }

        public static void Apply(this MobileSession source, MobileSessionRecord target)
        {
            target.AccessTokenHash = source.AccessTokenHash;
            target.AccessTokenExpiresUtc = source.AccessTokenExpiresUtc;
            target.RefreshTokenHash = source.RefreshTokenHash;
            target.RefreshTokenExpiresUtc = source.RefreshTokenExpiresUtc;
            target.LastUsedUtc = source.LastUsedUtc;
            target.RevokedUtc = source.RevokedUtc;
        }
    }
}
