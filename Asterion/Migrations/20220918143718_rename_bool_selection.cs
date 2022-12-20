using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    public partial class rename_bool_selection : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideChannelSelection",
                table: "Guilds");

            migrationBuilder.AddColumn<bool>(
                name: "ShowChannelSelection",
                table: "Guilds",
                type: "INTEGER",
                nullable: true,
                defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowChannelSelection",
                table: "Guilds");

            migrationBuilder.AddColumn<bool>(
                name: "HideChannelSelection",
                table: "Guilds",
                type: "INTEGER",
                nullable: true,
                defaultValue: false);
        }
    }
}
