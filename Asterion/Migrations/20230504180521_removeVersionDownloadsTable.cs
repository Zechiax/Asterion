using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class removeVersionDownloadsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectDownloads");

            migrationBuilder.AddColumn<int>(
                name: "Followers",
                table: "TotalDownloads",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Followers",
                table: "TotalDownloads");

            migrationBuilder.CreateTable(
                name: "ProjectDownloads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Downloads = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectDownloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectDownloads_ModrinthProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModrinthProjects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDownloads_ProjectId",
                table: "ProjectDownloads",
                column: "ProjectId");
        }
    }
}
