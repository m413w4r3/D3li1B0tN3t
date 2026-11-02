using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace D3B.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Placements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Placements",
                columns: table => new
                {
                    FromMapId = table.Column<long>(type: "INTEGER", nullable: false),
                    FromAreaId = table.Column<int>(type: "INTEGER", nullable: true),
                    FromSubAreaId = table.Column<int>(type: "INTEGER", nullable: true),
                    PosX = table.Column<int>(type: "INTEGER", nullable: true),
                    PosY = table.Column<int>(type: "INTEGER", nullable: true),
                    MapId = table.Column<long>(type: "INTEGER", nullable: false),
                    RedCells = table.Column<string>(type: "TEXT", nullable: false),
                    BlueCells = table.Column<string>(type: "TEXT", nullable: false),
                    Seen = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Placements", x => new { x.FromMapId, x.MapId, x.RedCells, x.BlueCells });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Placements");
        }
    }
}
