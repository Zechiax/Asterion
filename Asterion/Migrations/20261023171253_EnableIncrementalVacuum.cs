using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Asterion.Migrations
{
    /// <inheritdoc />
    public partial class EnableIncrementalVacuum : Migration
    {
        // VACUUM shrinks the file after the dropped statistics and is the only way to switch auto_vacuum on an
        // existing database. Neither can run inside a transaction, so they are alone here, and running them again
        // after an interrupted run is harmless.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA auto_vacuum = INCREMENTAL;", suppressTransaction: true);
            migrationBuilder.Sql("VACUUM;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("PRAGMA auto_vacuum = NONE;", suppressTransaction: true);
            migrationBuilder.Sql("VACUUM;", suppressTransaction: true);
        }
    }
}
