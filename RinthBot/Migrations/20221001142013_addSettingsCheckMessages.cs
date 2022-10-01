using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RinthBot.Migrations
{
    public partial class addSettingsCheckMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CheckMessagesForModrinthLink",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: true,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckMessagesForModrinthLink",
                table: "GuildSettings");
        }
    }
}
