using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class addloadertype_entry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SerializedLoaderFilter",
                table: "ModrinthEntries",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SerializedLoaderFilter",
                table: "ModrinthEntries");
        }
    }
}
