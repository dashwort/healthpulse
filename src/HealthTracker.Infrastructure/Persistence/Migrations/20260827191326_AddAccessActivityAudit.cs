using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessActivityAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllowedUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    OccurredUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceIpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccessActivities_AllowedUserId_OccurredUtc",
                table: "AccessActivities",
                columns: new[] { "AllowedUserId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessActivities_OccurredUtc",
                table: "AccessActivities",
                column: "OccurredUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccessActivities");
        }
    }
}
