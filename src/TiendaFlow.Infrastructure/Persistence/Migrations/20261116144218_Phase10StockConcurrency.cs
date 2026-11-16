using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10StockConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "stock",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "stock");
        }
    }
}
