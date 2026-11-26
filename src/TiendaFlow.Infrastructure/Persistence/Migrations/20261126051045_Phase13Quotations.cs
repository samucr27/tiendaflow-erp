using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase13Quotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "quotation_request",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    supplier_id = table.Column<int>(type: "int", nullable: false),
                    product_sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(14,2)", precision: 14, scale: 2, nullable: false),
                    requested_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    valid_until = table.Column<DateOnly>(type: "date", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    decided_by_user_id = table.Column<int>(type: "int", nullable: true),
                    decided_by_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    rejection_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    purchase_order_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quotation_request", x => x.id);
                    table.ForeignKey(
                        name: "FK_quotation_request_app_user_decided_by_user_id",
                        column: x => x.decided_by_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotation_request_product_product_sku",
                        column: x => x.product_sku,
                        principalTable: "product",
                        principalColumn: "sku",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotation_request_purchase_order_purchase_order_id",
                        column: x => x.purchase_order_id,
                        principalTable: "purchase_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_quotation_request_supplier_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "supplier",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quotation_request_decided_by_user_id",
                table: "quotation_request",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_request_product_sku",
                table: "quotation_request",
                column: "product_sku");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_request_purchase_order_id",
                table: "quotation_request",
                column: "purchase_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_quotation_request_status_requested_at",
                table: "quotation_request",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "IX_quotation_request_supplier_id",
                table: "quotation_request",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quotation_request");
        }
    }
}
