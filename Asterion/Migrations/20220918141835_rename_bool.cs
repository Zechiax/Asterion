using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    public partial class rename_bool : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelSelectionAfterSubscribe",
                table: "Guilds");

            migrationBuilder.AlterColumn<bool>(
                name: "RemoveOnLeave",
                table: "Guilds",
                type: "INTEGER",
                nullable: true,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "Guilds",
                type: "INTEGER",
                nullable: true,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "HideChannelSelection",
                table: "Guilds",
                type: "INTEGER",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideChannelSelection",
                table: "Guilds");

            migrationBuilder.AlterColumn<bool>(
                name: "RemoveOnLeave",
                table: "Guilds",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldNullable: true,
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Active",
                table: "Guilds",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldNullable: true,
                oldDefaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChannelSelectionAfterSubscribe",
                table: "Guilds",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }
    }
}
