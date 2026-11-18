using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase13HrAttendanceAndShifts : Migration
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
                    store_id = table.Column<int>(type: "int", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    check_in = table.Column<DateTime>(type: "datetime2", nullable: true),
                    check_out = table.Column<DateTime>(type: "datetime2", nullable: true),
                    novelty = table.Column<int>(type: "int", nullable: true),
                    novelty_reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_attendance_record_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shift_template",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_assignment",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<int>(type: "int", nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    shift_template_id = table.Column<int>(type: "int", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_assignment", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_assignment_employee_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_assignment_shift_template_shift_template_id",
                        column: x => x.shift_template_id,
                        principalTable: "shift_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_assignment_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_record_date_store",
                table: "attendance_record",
                columns: new[] { "work_date", "store_id" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_record_employee_date",
                table: "attendance_record",
                columns: new[] { "employee_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attendance_record_store_id",
                table: "attendance_record",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignment_date_store",
                table: "shift_assignment",
                columns: new[] { "work_date", "store_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignment_employee_date",
                table: "shift_assignment",
                columns: new[] { "employee_id", "work_date" });

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignment_shift_template_id",
                table: "shift_assignment",
                column: "shift_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignment_store_id",
                table: "shift_assignment",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_template_name",
                table: "shift_template",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_record");

            migrationBuilder.DropTable(
                name: "shift_assignment");

            migrationBuilder.DropTable(
                name: "shift_template");
        }
    }
}
