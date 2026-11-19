using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class MigrateDefaultReleaseFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Untouched default (Alpha | Beta | Release = 7) becomes NULL so existing rows inherit server defaults
            migrationBuilder.Sql("UPDATE \"ModrinthEntries\" SET \"ReleaseFilter\" = NULL WHERE \"ReleaseFilter\" = 7;");
            migrationBuilder.Sql("UPDATE \"ModrinthFollows\" SET \"ReleaseFilter\" = NULL WHERE \"ReleaseFilter\" = 7;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"ModrinthEntries\" SET \"ReleaseFilter\" = 7 WHERE \"ReleaseFilter\" IS NULL;");
            migrationBuilder.Sql("UPDATE \"ModrinthFollows\" SET \"ReleaseFilter\" = 7 WHERE \"ReleaseFilter\" IS NULL;");
        }
    }
}
