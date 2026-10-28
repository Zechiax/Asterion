using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class UniqueSubscriptionPerGuild : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep the oldest of duplicate subscriptions, the unique index would fail on them
            migrationBuilder.Sql(
                "DELETE FROM ModrinthEntries WHERE EntryId NOT IN " +
                "(SELECT MIN(EntryId) FROM ModrinthEntries GROUP BY GuildId, ProjectId);");

            // Arrays of removed guilds were left behind, the guild was deleted without its array
            migrationBuilder.Sql("DELETE FROM Arrays WHERE ArrayId NOT IN (SELECT ModrinthArrayId FROM Guilds);");

            migrationBuilder.DropIndex(
                name: "IX_ModrinthEntries_GuildId",
                table: "ModrinthEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ModrinthEntries_GuildId_ProjectId",
                table: "ModrinthEntries",
                columns: new[] { "GuildId", "ProjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModrinthEntries_GuildId_ProjectId",
                table: "ModrinthEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ModrinthEntries_GuildId",
                table: "ModrinthEntries",
                column: "GuildId");
        }
    }
}
