using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalAccessTokensAndMcpAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "McpAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PersonalAccessTokenId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllowedUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OccurredUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalAccessTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AllowedUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUsedUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    RevokedUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalAccessTokens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditLogs_AllowedUserId_OccurredUtc",
                table: "McpAuditLogs",
                columns: new[] { "AllowedUserId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_McpAuditLogs_PersonalAccessTokenId_OccurredUtc",
                table: "McpAuditLogs",
                columns: new[] { "PersonalAccessTokenId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAccessTokens_AllowedUserId_RevokedUtc",
                table: "PersonalAccessTokens",
                columns: new[] { "AllowedUserId", "RevokedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAccessTokens_Hash",
                table: "PersonalAccessTokens",
                column: "Hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "McpAuditLogs");

            migrationBuilder.DropTable(
                name: "PersonalAccessTokens");
        }
    }
}
