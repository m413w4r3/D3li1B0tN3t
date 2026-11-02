using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D3B.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Regrowths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Regrowths",
                columns: table => new
                {
                    SkillId = table.Column<int>(type: "INTEGER", nullable: false),
                    LongestMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    Seen = table.Column<int>(type: "INTEGER", nullable: false),
                    AtMostMilliseconds = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Regrowths", x => x.SkillId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Regrowths");
        }
    }
}
