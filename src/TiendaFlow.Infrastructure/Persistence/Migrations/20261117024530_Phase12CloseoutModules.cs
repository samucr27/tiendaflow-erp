using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase12CloseoutModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attendance_record",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<int>(type: "int", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    attendance_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    check_in = table.Column<TimeOnly>(type: "time", nullable: true),
                    check_out = table.Column<TimeOnly>(type: "time", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_record", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_record_employee_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employment_contract",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<int>(type: "int", nullable: false),
                    contract_type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    salary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    extensions = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employment_contract", x => x.id);
                    table.ForeignKey(
                        name: "FK_employment_contract_employee_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_points_entry",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    points = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    invoice_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_points_entry", x => x.id);
                    table.ForeignKey(
                        name: "FK_loyalty_points_entry_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "marketing_campaign",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    segment = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    cancelled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketing_campaign", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_run",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    days_worked = table.Column<int>(type: "int", nullable: false),
                    risk_class = table.Column<int>(type: "int", nullable: false),
                    created_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pqrs_case",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    pqrs_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    customer_id = table.Column<int>(type: "int", nullable: true),
                    contact = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    responsible = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    response = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "role_permission",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "int", nullable: false),
                    permission = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permission", x => new { x.role_id, x.permission });
                    table.ForeignKey(
                        name: "FK_role_permission_role_role_id",
                        column: x => x.role_id,
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_return",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    invoice_id = table.Column<int>(type: "int", nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    authorized_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_sales_invoice_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "sales_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sales_return_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_shift",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    employee_id = table.Column<int>(type: "int", nullable: false),
                    shift_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_shift", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_shift_employee_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_shift_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_run_line",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_run_id = table.Column<int>(type: "int", nullable: false),
                    employee_id = table.Column<int>(type: "int", nullable: false),
                    employee_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    days_applied = table.Column<int>(type: "int", nullable: false),
                    gross_earnings = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    deductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    net_pay = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    employer_cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payroll_run_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_run_line_payroll_run_payroll_run_id",
                        column: x => x.payroll_run_id,
                        principalTable: "payroll_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sales_return_line",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    sales_return_id = table.Column<int>(type: "int", nullable: false),
                    product_sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_return_line", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_return_line_sales_return_sales_return_id",
                        column: x => x.sales_return_id,
                        principalTable: "sales_return",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_record_employee_id_work_date",
                table: "attendance_record",
                columns: new[] { "employee_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employment_contract_employee_id_status",
                table: "employment_contract",
                columns: new[] { "employee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_points_entry_customer_id",
                table: "loyalty_points_entry",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_points_entry_invoice_id",
                table: "loyalty_points_entry",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_run_period",
                table: "payroll_run",
                column: "period",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payroll_run_line_payroll_run_id",
                table: "payroll_run_line",
                column: "payroll_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrs_case_customer_id",
                table: "pqrs_case",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_pqrs_case_status_due_date",
                table: "pqrs_case",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_invoice_id",
                table: "sales_return",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_store_id",
                table: "sales_return",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_return_line_sales_return_id",
                table: "sales_return_line",
                column: "sales_return_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_shift_employee_id",
                table: "work_shift",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_shift_store_id_shift_date",
                table: "work_shift",
                columns: new[] { "store_id", "shift_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_record");

            migrationBuilder.DropTable(
                name: "employment_contract");

            migrationBuilder.DropTable(
                name: "loyalty_points_entry");

            migrationBuilder.DropTable(
                name: "marketing_campaign");

            migrationBuilder.DropTable(
                name: "payroll_run_line");

            migrationBuilder.DropTable(
                name: "pqrs_case");

            migrationBuilder.DropTable(
                name: "role_permission");

            migrationBuilder.DropTable(
                name: "sales_return_line");

            migrationBuilder.DropTable(
                name: "work_shift");

            migrationBuilder.DropTable(
                name: "payroll_run");

            migrationBuilder.DropTable(
                name: "sales_return");
        }
    }
}
