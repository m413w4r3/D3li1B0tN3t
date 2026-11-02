using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D3B.Storage.Migrations
{
    /// <inheritdoc />
    public partial class NoStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The strategies' profiles go with them, their characters' column first set back to none by its reference (D243).
            migrationBuilder.Sql("DELETE FROM \"Profiles\" WHERE \"ActivityId\" = 'strategy';");
            migrationBuilder.Sql("DELETE FROM \"Presets\" WHERE \"ActivityId\" = 'strategy';");

            migrationBuilder.DropIndex(
                name: "IX_Characters_StrategyId",
                table: "Characters");

            // SQLite drops a column whose reference is its own, inside the migration's transaction; EF would rebuild the
            // table outside it (D240, D243).
            migrationBuilder.Sql("ALTER TABLE \"Characters\" DROP COLUMN \"StrategyId\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The strategies deleted going up do not come back: every character goes down with none.
            migrationBuilder.Sql("ALTER TABLE \"Characters\" ADD COLUMN \"StrategyId\" INTEGER NULL REFERENCES \"Profiles\" (\"Id\") ON DELETE SET NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_StrategyId",
                table: "Characters",
                column: "StrategyId");
        }
    }
}
