using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileAuthorizationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CodeChallenge = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RedirectUri = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpiresUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AuthorizationCodeHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    AuthorizationCodeExpiresUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ConsumedUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileAuthorizationRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MobileSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessTokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AccessTokenExpiresUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RefreshTokenExpiresUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUsedUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    RevokedUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileAuthorizationRequests_AuthorizationCodeHash",
                table: "MobileAuthorizationRequests",
                column: "AuthorizationCodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileSessions_AccessTokenHash",
                table: "MobileSessions",
                column: "AccessTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MobileSessions_ApplicationUserId_RevokedUtc",
                table: "MobileSessions",
                columns: new[] { "ApplicationUserId", "RevokedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MobileSessions_RefreshTokenHash",
                table: "MobileSessions",
                column: "RefreshTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MobileAuthorizationRequests");

            migrationBuilder.DropTable(
                name: "MobileSessions");
        }
    }
}
