using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Invoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sales_invoice",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    customer_document = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    issue_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_line",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    product_sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_invoice_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_invoice_line_sales_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "sales_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_customer_document_issue_date",
                table: "sales_invoice",
                columns: new[] { "customer_document", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_invoice_line_invoice_id_product_sku",
                table: "sales_invoice_line",
                columns: new[] { "invoice_id", "product_sku" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_invoice_line");

            migrationBuilder.DropTable(
                name: "sales_invoice");
        }
    }
}
