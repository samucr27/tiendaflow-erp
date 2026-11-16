using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11PosSalesAndInvoiceStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "store_id",
                table: "sales_invoice",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pos_sale",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    cashier_user_id = table.Column<int>(type: "int", nullable: true),
                    sold_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_sale", x => x.id);
                    table.ForeignKey(
                        name: "FK_pos_sale_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pos_sale_line",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    pos_sale_id = table.Column<int>(type: "int", nullable: false),
                    product_sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    product_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pos_sale_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_pos_sale_line_pos_sale_pos_sale_id",
                        column: x => x.pos_sale_id,
                        principalTable: "pos_sale",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_store_id",
                table: "sales_invoice",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_pos_sale_sold_at_store",
                table: "pos_sale",
                columns: new[] { "sold_at", "store_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_sale_store_id",
                table: "pos_sale",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_pos_sale_line_pos_sale_id",
                table: "pos_sale_line",
                column: "pos_sale_id");

            migrationBuilder.CreateIndex(
                name: "IX_pos_sale_line_product_sku",
                table: "pos_sale_line",
                column: "product_sku");

            migrationBuilder.AddForeignKey(
                name: "FK_sales_invoice_store_store_id",
                table: "sales_invoice",
                column: "store_id",
                principalTable: "store",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sales_invoice_store_store_id",
                table: "sales_invoice");

            migrationBuilder.DropTable(
                name: "pos_sale_line");

            migrationBuilder.DropTable(
                name: "pos_sale");

            migrationBuilder.DropIndex(
                name: "IX_sales_invoice_store_id",
                table: "sales_invoice");

            migrationBuilder.DropColumn(
                name: "store_id",
                table: "sales_invoice");
        }
    }
}
