using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class AddTotalDownloadsProjectTimestampIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TotalDownloads_ProjectId",
                table: "TotalDownloads");

            migrationBuilder.CreateIndex(
                name: "IX_TotalDownloads_ProjectId_Timestamp",
                table: "TotalDownloads",
                columns: new[] { "ProjectId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TotalDownloads_ProjectId_Timestamp",
                table: "TotalDownloads");

            migrationBuilder.CreateIndex(
                name: "IX_TotalDownloads_ProjectId",
                table: "TotalDownloads",
                column: "ProjectId");
        }
    }
}
