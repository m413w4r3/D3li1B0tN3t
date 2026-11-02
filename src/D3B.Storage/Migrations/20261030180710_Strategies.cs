using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D3B.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Strategies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF rebuilds a SQLite table to drop a column or add a foreign key, outside the migration's transaction; SQLite
            // drops a plain column and adds one with its reference itself, inside it (D240).
            migrationBuilder.Sql("ALTER TABLE \"Characters\" DROP COLUMN \"FightsAlone\";");
            migrationBuilder.Sql("ALTER TABLE \"Characters\" ADD COLUMN \"StrategyId\" INTEGER NULL REFERENCES \"Profiles\" (\"Id\") ON DELETE SET NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_StrategyId",
                table: "Characters",
                column: "StrategyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite drops no column a foreign key holds: EF rebuilds the table, as it would have to go up.
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Profiles_StrategyId",
                table: "Characters");

            migrationBuilder.DropIndex(
                name: "IX_Characters_StrategyId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "StrategyId",
                table: "Characters");

            migrationBuilder.AddColumn<bool>(
                name: "FightsAlone",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
