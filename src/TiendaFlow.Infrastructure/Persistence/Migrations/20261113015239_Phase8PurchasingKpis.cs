using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8PurchasingKpis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "promised_date",
                table: "purchase_order",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "received_date",
                table: "purchase_order",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_supplier",
                columns: table => new
                {
                    product_sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    supplier_id = table.Column<int>(type: "int", nullable: false),
                    last_cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    is_default = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_supplier", x => new { x.product_sku, x.supplier_id });
                    table.ForeignKey(
                        name: "FK_product_supplier_product_product_sku",
                        column: x => x.product_sku,
                        principalTable: "product",
                        principalColumn: "sku",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_supplier_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_supplier_default",
                table: "product_supplier",
                column: "product_sku",
                unique: true,
                filter: "[is_default] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_product_supplier_supplier_id",
                table: "product_supplier",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_supplier");

            migrationBuilder.DropColumn(
                name: "promised_date",
                table: "purchase_order");

            migrationBuilder.DropColumn(
                name: "received_date",
                table: "purchase_order");
        }
    }
}
