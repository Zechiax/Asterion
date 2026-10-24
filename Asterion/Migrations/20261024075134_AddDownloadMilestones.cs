using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "VersionId",
                table: "PendingNotifications",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<string>(
                name: "SerializedVersion",
                table: "PendingNotifications",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<long>(
                name: "Milestone",
                table: "PendingNotifications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MilestoneCheckedDownloads",
                table: "ModrinthProjects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "MilestoneChannel",
                table: "ModrinthFollows",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "MilestoneChannel",
                table: "ModrinthEntries",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyMilestones",
                table: "ModrinthEntries",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<ulong>(
                name: "MilestoneChannel",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MilestoneLadder",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<ulong>(
                name: "MilestonePingRole",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MilestonesEnabled",
                table: "GuildSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Existing follows get the milestone event, the server setting alone decides if they are sent
            migrationBuilder.Sql("UPDATE \"ModrinthFollows\" SET \"Events\" = \"Events\" | 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Milestone rows have no version, the columns can't be made required with them
            migrationBuilder.Sql("DELETE FROM \"PendingNotifications\" WHERE \"Kind\" = 2;");
            migrationBuilder.Sql("UPDATE \"ModrinthFollows\" SET \"Events\" = \"Events\" & ~4;");

            migrationBuilder.DropColumn(
                name: "Milestone",
                table: "PendingNotifications");

            migrationBuilder.DropColumn(
                name: "MilestoneCheckedDownloads",
                table: "ModrinthProjects");

            migrationBuilder.DropColumn(
                name: "MilestoneChannel",
                table: "ModrinthFollows");

            migrationBuilder.DropColumn(
                name: "MilestoneChannel",
                table: "ModrinthEntries");

            migrationBuilder.DropColumn(
                name: "NotifyMilestones",
                table: "ModrinthEntries");

            migrationBuilder.DropColumn(
                name: "MilestoneChannel",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "MilestoneLadder",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "MilestonePingRole",
                table: "GuildSettings");

            migrationBuilder.DropColumn(
                name: "MilestonesEnabled",
                table: "GuildSettings");

            migrationBuilder.AlterColumn<string>(
                name: "VersionId",
                table: "PendingNotifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SerializedVersion",
                table: "PendingNotifications",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
