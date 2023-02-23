using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class addChangelogSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MessageStyle",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<long>(
                name: "ChangeLogMaxLength",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2000L);

            migrationBuilder.AddColumn<int>(
                name: "ChangelogStyle",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangeLogMaxLength",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "ChangelogStyle",
                table: "GuildSettings");

            migrationBuilder.AlterColumn<int>(
                name: "MessageStyle",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 0);
        }
    }
}
