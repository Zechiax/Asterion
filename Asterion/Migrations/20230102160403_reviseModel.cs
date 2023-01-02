using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class reviseModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the GuildModrinthEntries table
            migrationBuilder.CreateTable(
                name: "GuildModrinthEntries",
                columns: table => new
                {
                    EntryId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectType = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomUpdateChannel = table.Column<ulong>(type: "INTEGER", nullable: true),
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildModrinthEntries", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_GuildModrinthEntries_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildModrinthEntries_ModrinthProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModrinthProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Copy data from the ModrinthEntries table to the GuildModrinthEntries table
            migrationBuilder.Sql("INSERT INTO GuildModrinthEntries SELECT * FROM ModrinthEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Guilds_Arrays_ModrinthArrayId",
                table: "Guilds");

            migrationBuilder.DropTable(
                name: "ModrinthEntries");

            migrationBuilder.DropTable(
                name: "Arrays");

            migrationBuilder.DropIndex(
                name: "IX_Guilds_ModrinthArrayId",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "ModrinthArrayId",
                table: "Guilds");

            migrationBuilder.CreateIndex(
                name: "IX_GuildModrinthEntries_GuildId",
                table: "GuildModrinthEntries",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildModrinthEntries_ProjectId",
                table: "GuildModrinthEntries",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "Guilds",
                type: "INTEGER",
                nullable: true,
                defaultValue: true);

            migrationBuilder.AddColumn<ulong>(
                name: "ModrinthArrayId",
                table: "Guilds",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.CreateTable(
                name: "Arrays",
                columns: table => new
                {
                    ArrayId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arrays", x => x.ArrayId);
                });

            migrationBuilder.CreateTable(
                name: "ModrinthEntries",
                columns: table => new
                {
                    EntryId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArrayId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CustomUpdateChannel = table.Column<ulong>(type: "INTEGER", nullable: true)
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
            
            // Migrate data from the GuildModrinthEntries table to the ModrinthEntries table
            migrationBuilder.Sql("INSERT INTO ModrinthEntries SELECT * FROM GuildModrinthEntries");
            
            migrationBuilder.DropTable(
                name: "GuildModrinthEntries");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Guilds_Arrays_ModrinthArrayId",
                table: "Guilds",
                column: "ModrinthArrayId",
                principalTable: "Arrays",
                principalColumn: "ArrayId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
