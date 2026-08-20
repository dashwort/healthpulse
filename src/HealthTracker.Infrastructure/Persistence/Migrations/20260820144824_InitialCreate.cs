using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UnitCategory = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NormalizedUnit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    AllowedUnits = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Templates", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(
                        type: "TEXT",
                        precision: 18,
                        scale: 6,
                        nullable: false
                    ),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Readings_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "TrackedTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackedTemplates_Templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "Templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Readings_TemplateId",
                table: "Readings",
                column: "TemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Readings_UserId_TemplateId_RecordedAtUtc",
                table: "Readings",
                columns: ["UserId", "TemplateId", "RecordedAtUtc"]
            );

            migrationBuilder.CreateIndex(
                name: "IX_Templates_Code",
                table: "Templates",
                column: "Code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Templates_OwnerUserId_DeletedUtc",
                table: "Templates",
                columns: ["OwnerUserId", "DeletedUtc"]
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrackedTemplates_TemplateId",
                table: "TrackedTemplates",
                column: "TemplateId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrackedTemplates_UserId_TemplateId",
                table: "TrackedTemplates",
                columns: ["UserId", "TemplateId"],
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_Subject",
                table: "Users",
                column: "Subject",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Readings");

            migrationBuilder.DropTable(name: "TrackedTemplates");

            migrationBuilder.DropTable(name: "Users");

            migrationBuilder.DropTable(name: "Templates");
        }
    }
}
