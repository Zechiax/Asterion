using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class addcustompingrole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "CustomPingRole",
                table: "ModrinthEntries",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomPingRole",
                table: "ModrinthEntries");
        }
    }
}
