using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Security : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve the old free-text user id, then add the numeric user id
            // the new security model uses. Existing audit rows keep their data.
            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "audit_log",
                newName: "legacy_user_id");

            migrationBuilder.AlterColumn<string>(
                name: "legacy_user_id",
                table: "audit_log",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "audit_log",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ip",
                table: "audit_log",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "audit_log",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "inventory");

            migrationBuilder.CreateTable(
                name: "role",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "store",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_store", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "employee",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    store_id = table.Column<int>(type: "int", nullable: false),
                    id_number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    position = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    contract_type = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    salary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<int>(type: "int", nullable: true),
                    role_id = table.Column<int>(type: "int", nullable: false),
                    email = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    two_factor = table.Column<bool>(type: "bit", nullable: false),
                    active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id);
                    table.ForeignKey(
                        name: "FK_app_user_employee_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employee",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_app_user_role_role_id",
                        column: x => x.role_id,
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_user_id",
                table: "audit_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_app_user_email",
                table: "app_user",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_employee_id",
                table: "app_user",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_app_user_role_id",
                table: "app_user",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_id_number",
                table: "employee",
                column: "id_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_store_id",
                table: "employee",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_name",
                table: "role",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_store_name",
                table: "store",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_log_app_user_user_id",
                table: "audit_log",
                column: "user_id",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_log_app_user_user_id",
                table: "audit_log");

            migrationBuilder.DropIndex(
                name: "IX_audit_log_user_id",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "ip",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "module",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "audit_log");

            migrationBuilder.RenameColumn(
                name: "legacy_user_id",
                table: "audit_log",
                newName: "user_id");

            migrationBuilder.AlterColumn<string>(
                name: "user_id",
                table: "audit_log",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "employee");

            migrationBuilder.DropTable(
                name: "role");

            migrationBuilder.DropTable(
                name: "store");
        }
    }
}
