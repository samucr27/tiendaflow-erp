using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6Stores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_purchase_order_store_id",
                table: "purchase_order");

            migrationBuilder.DropIndex(
                name: "IX_employee_store_id",
                table: "employee");

            migrationBuilder.CreateIndex(
                name: "IX_stock_store_quantity",
                table: "stock",
                columns: new[] { "store_id", "quantity" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_store_status",
                table: "purchase_order",
                columns: new[] { "store_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_store_active",
                table: "employee",
                columns: new[] { "store_id", "active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_store_quantity",
                table: "stock");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_store_status",
                table: "purchase_order");

            migrationBuilder.DropIndex(
                name: "IX_employee_store_active",
                table: "employee");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_store_id",
                table: "purchase_order",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_store_id",
                table: "employee",
                column: "store_id");
        }
    }
}
