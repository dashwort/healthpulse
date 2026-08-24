using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUrateUnit : Migration
    {
        private const string UrateTemplateId = "0b4b7051-b360-4d2d-9f36-0776baf95d01";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "Readings"
                SET "Value" = "Value" * 1000.0,
                    "Unit" = 'umol/L'
                WHERE lower("TemplateId") = lower('{UrateTemplateId}')
                  AND lower("Unit") = 'mmol/l';

                UPDATE "Templates"
                SET "NormalizedUnit" = 'umol/L',
                    "AllowedUnits" = 'umol/L|mg/dL'
                WHERE lower("Id") = lower('{UrateTemplateId}')
                  AND "OwnerUserId" IS NULL
                  AND "Code" = 'urate';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE "Readings"
                SET "Value" = "Value" / 1000.0,
                    "Unit" = 'mmol/L'
                WHERE lower("TemplateId") = lower('{UrateTemplateId}')
                  AND lower("Unit") = 'umol/l';

                UPDATE "Templates"
                SET "NormalizedUnit" = 'mmol/L',
                    "AllowedUnits" = 'mmol/L|mg/dL'
                WHERE lower("Id") = lower('{UrateTemplateId}')
                  AND "OwnerUserId" IS NULL
                  AND "Code" = 'urate';
                """);
        }
    }
}
