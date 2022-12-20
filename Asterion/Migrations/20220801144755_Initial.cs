using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Arrays",
                columns: table => new
                {
                    ArrayId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arrays", x => x.ArrayId);
                });

            migrationBuilder.CreateTable(
                name: "ModrinthProjects",
                columns: table => new
                {
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    LastCheckVersion = table.Column<string>(type: "TEXT", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModrinthProjects", x => x.ProjectId);
                });

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    UpdateChannel = table.Column<ulong>(type: "INTEGER", nullable: true),
                    MessageStyle = table.Column<ulong>(type: "INTEGER", nullable: false),
                    RemoveOnLeave = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: true),
                    Active = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: true),
                    PingRole = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ManageRole = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ModrinthArrayId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_Guilds_Arrays_ModrinthArrayId",
                        column: x => x.ModrinthArrayId,
                        principalTable: "Arrays",
                        principalColumn: "ArrayId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModrinthEntries",
                columns: table => new
                {
                    EntryId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArrayId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    CustomUpdateChannel = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModrinthEntries", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_ModrinthEntries_Arrays_ArrayId",
                        column: x => x.ArrayId,
                        principalTable: "Arrays",
                        principalColumn: "ArrayId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModrinthEntries_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModrinthEntries_ModrinthProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModrinthProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_ModrinthArrayId",
                table: "Guilds",
                column: "ModrinthArrayId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModrinthEntries_ArrayId",
                table: "ModrinthEntries",
                column: "ArrayId");

            migrationBuilder.CreateIndex(
                name: "IX_ModrinthEntries_GuildId",
                table: "ModrinthEntries",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_ModrinthEntries_ProjectId",
                table: "ModrinthEntries",
                column: "ProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModrinthEntries");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropTable(
                name: "ModrinthProjects");

            migrationBuilder.DropTable(
                name: "Arrays");
        }
    }
}
