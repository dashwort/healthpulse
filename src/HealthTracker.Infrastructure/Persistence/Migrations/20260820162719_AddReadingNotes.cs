using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Readings",
                type: "TEXT",
                maxLength: 140,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Note", table: "Readings");
        }
    }
}
