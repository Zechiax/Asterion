using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingNotificationsAndWebhookUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "ModrinthEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PendingNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    VersionId = table.Column<string>(type: "TEXT", nullable: false),
                    SerializedProject = table.Column<string>(type: "TEXT", nullable: false),
                    SerializedVersion = table.Column<string>(type: "TEXT", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    EntryId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingNotifications_ModrinthEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "ModrinthEntries",
                        principalColumn: "EntryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PendingNotifications_ModrinthProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModrinthProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingNotifications_EntryId",
                table: "PendingNotifications",
                column: "EntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingNotifications_ProjectId",
                table: "PendingNotifications",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingNotifications_Status_LastAttemptAt",
                table: "PendingNotifications",
                columns: new[] { "Status", "LastAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingNotifications_Status_NextRetryAt",
                table: "PendingNotifications",
                columns: new[] { "Status", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingNotifications");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "ModrinthEntries");
        }
    }
}
