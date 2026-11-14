using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9InventoryDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_count",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    adjusted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_count", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_count_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transfer",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    origin_store_id = table.Column<int>(type: "int", nullable: false),
                    destination_store_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    received_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transfer", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_store_destination_store_id",
                        column: x => x.destination_store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_store_origin_store_id",
                        column: x => x.origin_store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_count_line",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    count_id = table.Column<int>(type: "int", nullable: false),
                    product_sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    system_quantity = table.Column<int>(type: "int", nullable: false),
                    counted_quantity = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_count_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_count_line_inventory_count_count_id",
                        column: x => x.count_id,
                        principalTable: "inventory_count",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_count_line_product_product_sku",
                        column: x => x.product_sku,
                        principalTable: "product",
                        principalColumn: "sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transfer_line",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    transfer_id = table.Column<int>(type: "int", nullable: false),
                    product_sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_transfer_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_line_inventory_transfer_transfer_id",
                        column: x => x.transfer_id,
                        principalTable: "inventory_transfer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_line_product_product_sku",
                        column: x => x.product_sku,
                        principalTable: "product",
                        principalColumn: "sku",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_created_at",
                table: "inventory_count",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_store_status",
                table: "inventory_count",
                columns: new[] { "store_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_line_count_sku",
                table: "inventory_count_line",
                columns: new[] { "count_id", "product_sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_line_product_sku",
                table: "inventory_count_line",
                column: "product_sku");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_destination_status",
                table: "inventory_transfer",
                columns: new[] { "destination_store_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_origin_status",
                table: "inventory_transfer",
                columns: new[] { "origin_store_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_line_product_sku",
                table: "inventory_transfer_line",
                column: "product_sku");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_line_transfer_sku",
                table: "inventory_transfer_line",
                columns: new[] { "transfer_id", "product_sku" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_count_line");

            migrationBuilder.DropTable(
                name: "inventory_transfer_line");

            migrationBuilder.DropTable(
                name: "inventory_count");

            migrationBuilder.DropTable(
                name: "inventory_transfer");
        }
    }
}
