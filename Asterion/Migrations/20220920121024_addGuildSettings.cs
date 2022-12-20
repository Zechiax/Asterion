using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    public partial class addGuildSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RemoveOnLeave",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "ShowChannelSelection",
                table: "Guilds");

            migrationBuilder.DropColumn(
                name: "UpdateChannel",
                table: "Guilds");

            migrationBuilder.RenameColumn(
                name: "MessageStyle",
                table: "Guilds",
                newName: "GuildSettingsId");

            migrationBuilder.CreateTable(
                name: "GuildSettings",
                columns: table => new
                {
                    GuildSettingsId = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ShowChannelSelection = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: true),
                    RemoveOnLeave = table.Column<bool>(type: "INTEGER", nullable: true, defaultValue: true),
                    MessageStyle = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => x.GuildSettingsId);
                    table.ForeignKey(
                        name: "FK_GuildSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildSettings_GuildId",
                table: "GuildSettings",
                column: "GuildId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildSettings");

            migrationBuilder.RenameColumn(
                name: "GuildSettingsId",
                table: "Guilds",
                newName: "MessageStyle");

            migrationBuilder.AddColumn<bool>(
                name: "RemoveOnLeave",
                table: "Guilds",
                type: "INTEGER",
                nullable: true,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowChannelSelection",
                table: "Guilds",
                type: "INTEGER",
                nullable: true,
                defaultValue: true);

            migrationBuilder.AddColumn<ulong>(
                name: "UpdateChannel",
                table: "Guilds",
                type: "INTEGER",
                nullable: true);
        }
    }
}
