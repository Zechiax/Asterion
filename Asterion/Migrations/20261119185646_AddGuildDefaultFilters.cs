using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildDefaultFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ReleaseFilter",
                table: "ModrinthFollows",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 7);

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseFilter",
                table: "ModrinthEntries",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 7);

            migrationBuilder.AddColumn<int>(
                name: "DefaultReleaseFilter",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<string>(
                name: "SerializedDefaultLoaderFilter",
                table: "GuildSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultReleaseFilter",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "SerializedDefaultLoaderFilter",
                table: "GuildSettings");

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseFilter",
                table: "ModrinthFollows",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReleaseFilter",
                table: "ModrinthEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: 7,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
