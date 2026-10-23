using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class ReworkProjectStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyProjectStats",
                columns: table => new
                {
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    Day = table.Column<long>(type: "INTEGER", nullable: false),
                    Downloads = table.Column<int>(type: "INTEGER", nullable: false),
                    Followers = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyProjectStats", x => new { x.ProjectId, x.Day });
                    table.ForeignKey(
                        name: "FK_DailyProjectStats_ModrinthProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModrinthProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HourlyProjectStats",
                columns: table => new
                {
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    Hour = table.Column<long>(type: "INTEGER", nullable: false),
                    Downloads = table.Column<int>(type: "INTEGER", nullable: false),
                    Followers = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HourlyProjectStats", x => new { x.ProjectId, x.Hour });
                    table.ForeignKey(
                        name: "FK_HourlyProjectStats_ModrinthProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModrinthProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Keep the last sample of each day, and of each hour from the last 30 days, the old rows have
            // TEXT timestamps, the new ones unix seconds of the bucket start
            migrationBuilder.Sql("""
                INSERT INTO DailyProjectStats (ProjectId, Day, Downloads, Followers)
                SELECT ProjectId, Bucket, Downloads, Followers
                FROM (SELECT ProjectId, Downloads, Followers,
                             CAST(strftime('%s', Timestamp) AS INTEGER) / 86400 * 86400 AS Bucket,
                             ROW_NUMBER() OVER (
                                 PARTITION BY ProjectId, CAST(strftime('%s', Timestamp) AS INTEGER) / 86400
                                 ORDER BY Timestamp DESC, Id DESC) AS Rank
                      FROM TotalDownloads
                      WHERE ProjectId IN (SELECT ProjectId FROM ModrinthProjects))
                WHERE Rank = 1;
                """);

            migrationBuilder.Sql("""
                INSERT INTO HourlyProjectStats (ProjectId, Hour, Downloads, Followers)
                SELECT ProjectId, Bucket, Downloads, Followers
                FROM (SELECT ProjectId, Downloads, Followers,
                             CAST(strftime('%s', Timestamp) AS INTEGER) / 3600 * 3600 AS Bucket,
                             ROW_NUMBER() OVER (
                                 PARTITION BY ProjectId, CAST(strftime('%s', Timestamp) AS INTEGER) / 3600
                                 ORDER BY Timestamp DESC, Id DESC) AS Rank
                      FROM TotalDownloads
                      WHERE Timestamp >= datetime('now', '-30 days')
                        AND ProjectId IN (SELECT ProjectId FROM ModrinthProjects))
                WHERE Rank = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DailyProjectStats_Day",
                table: "DailyProjectStats",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "IX_HourlyProjectStats_Hour",
                table: "HourlyProjectStats",
                column: "Hour");

            migrationBuilder.DropTable(
                name: "ModrinthInstanceStatistics");

            migrationBuilder.DropTable(
                name: "TotalDownloads");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModrinthInstanceStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Authors = table.Column<int>(type: "INTEGER", nullable: false),
                    Files = table.Column<int>(type: "INTEGER", nullable: false),
                    Projects = table.Column<int>(type: "INTEGER", nullable: false),
                    Versions = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModrinthInstanceStatistics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TotalDownloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    Downloads = table.Column<int>(type: "INTEGER", nullable: false),
                    Followers = table.Column<int>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TotalDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TotalDownloads_ModrinthProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModrinthProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TotalDownloads_ProjectId_Timestamp",
                table: "TotalDownloads",
                columns: new[] { "ProjectId", "Timestamp" });

            // Daily samples before the hourly window and every hourly one, as the old trim left them
            migrationBuilder.Sql("""
                INSERT INTO TotalDownloads (ProjectId, Downloads, Followers, Timestamp)
                SELECT ProjectId, Downloads, Followers, strftime('%Y-%m-%d %H:%M:%S', Day, 'unixepoch')
                FROM DailyProjectStats
                WHERE Day < (SELECT coalesce(min(Hour), 9223372036854775807) FROM HourlyProjectStats)
                UNION ALL
                SELECT ProjectId, Downloads, Followers, strftime('%Y-%m-%d %H:%M:%S', Hour, 'unixepoch')
                FROM HourlyProjectStats;
                """);

            migrationBuilder.DropTable(
                name: "DailyProjectStats");

            migrationBuilder.DropTable(
                name: "HourlyProjectStats");
        }
    }
}
