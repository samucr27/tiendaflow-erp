using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase14CrmLoyaltyPqrsCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    channel = table.Column<int>(type: "int", nullable: false),
                    segment = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_movement",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    points = table.Column<int>(type: "int", nullable: false),
                    amount_cop = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_movement", x => x.id);
                    table.ForeignKey(
                        name: "FK_loyalty_movement_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_loyalty_movement_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pqrs_case",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    subject = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    created_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    response = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    answered_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrs_case", x => x.id);
                    table.ForeignKey(
                        name: "FK_pqrs_case_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pqrs_case_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_dates",
                table: "campaign",
                columns: new[] { "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_name",
                table: "campaign",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_movement_customer_date",
                table: "loyalty_movement",
                columns: new[] { "customer_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_movement_date_store",
                table: "loyalty_movement",
                columns: new[] { "created_at", "store_id" });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_movement_store_id",
                table: "loyalty_movement",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_movement_type_reference",
                table: "loyalty_movement",
                columns: new[] { "type", "reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pqrs_case_customer_id",
                table: "pqrs_case",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrs_case_date_store",
                table: "pqrs_case",
                columns: new[] { "created_date", "store_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pqrs_case_status_due",
                table: "pqrs_case",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "IX_pqrs_case_store_id",
                table: "pqrs_case",
                column: "store_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign");

            migrationBuilder.DropTable(
                name: "loyalty_movement");

            migrationBuilder.DropTable(
                name: "pqrs_case");
        }
    }
}
