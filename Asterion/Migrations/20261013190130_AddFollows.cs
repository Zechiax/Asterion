using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class AddFollows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<ulong>(
                name: "EntryId",
                table: "PendingNotifications",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(ulong),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<int>(
                name: "FollowId",
                table: "PendingNotifications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "PendingNotifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ModrinthFollows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    TargetType = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<string>(type: "TEXT", nullable: false),
                    TargetName = table.Column<string>(type: "TEXT", nullable: false),
                    Events = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomUpdateChannel = table.Column<ulong>(type: "INTEGER", nullable: true),
                    CustomPingRole = table.Column<ulong>(type: "INTEGER", nullable: true),
                    NewProjectPingRole = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ReleaseFilter = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 7),
                    SerializedLoaderFilter = table.Column<string>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastSyncError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModrinthFollows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModrinthFollows_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FollowProjects",
                columns: table => new
                {
                    FollowId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    Ignored = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstSeen = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FollowProjects", x => new { x.FollowId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_FollowProjects_ModrinthFollows_FollowId",
                        column: x => x.FollowId,
                        principalTable: "ModrinthFollows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FollowProjects_ModrinthProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModrinthProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingNotifications_FollowId",
                table: "PendingNotifications",
                column: "FollowId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PendingNotifications_Target",
                table: "PendingNotifications",
                sql: "(\"EntryId\" IS NOT NULL) <> (\"FollowId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_FollowProjects_ProjectId",
                table: "FollowProjects",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ModrinthFollows_GuildId_TargetType_TargetId",
                table: "ModrinthFollows",
                columns: new[] { "GuildId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModrinthFollows_TargetType_TargetId",
                table: "ModrinthFollows",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PendingNotifications_ModrinthFollows_FollowId",
                table: "PendingNotifications",
                column: "FollowId",
                principalTable: "ModrinthFollows",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PendingNotifications_ModrinthFollows_FollowId",
                table: "PendingNotifications");

            migrationBuilder.DropTable(
                name: "FollowProjects");

            migrationBuilder.DropTable(
                name: "ModrinthFollows");

            migrationBuilder.DropIndex(
                name: "IX_PendingNotifications_FollowId",
                table: "PendingNotifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PendingNotifications_Target",
                table: "PendingNotifications");

            migrationBuilder.DropColumn(
                name: "FollowId",
                table: "PendingNotifications");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "PendingNotifications");

            migrationBuilder.AlterColumn<ulong>(
                name: "EntryId",
                table: "PendingNotifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul,
                oldClrType: typeof(ulong),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
