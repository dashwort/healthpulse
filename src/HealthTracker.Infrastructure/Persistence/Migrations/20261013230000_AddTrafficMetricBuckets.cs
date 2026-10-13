using HealthTracker.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTracker.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(HealthTrackerDbContext))]
    [Migration("20261013230000_AddTrafficMetricBuckets")]
    public partial class AddTrafficMetricBuckets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrafficMetricBuckets",
                columns: table => new
                {
                    BucketStartUtcTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    RouteTemplate = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 12, nullable: false),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    LatencyBand = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestCount = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalElapsedMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxElapsedMilliseconds = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_TrafficMetricBuckets",
                        x => new
                        {
                            x.BucketStartUtcTicks,
                            x.RouteTemplate,
                            x.Method,
                            x.StatusCode,
                            x.LatencyBand,
                        }
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_TrafficMetricBuckets_BucketStartUtcTicks",
                table: "TrafficMetricBuckets",
                column: "BucketStartUtcTicks"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TrafficMetricBuckets");
        }
    }
}
