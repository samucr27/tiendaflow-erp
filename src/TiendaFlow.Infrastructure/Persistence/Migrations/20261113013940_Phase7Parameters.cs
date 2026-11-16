using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7Parameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_parameter",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    parameter_key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    value_type = table.Column<int>(type: "int", nullable: false),
                    scope = table.Column<int>(type: "int", nullable: false),
                    store_id = table.Column<int>(type: "int", nullable: true),
                    value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    version = table.Column<int>(type: "int", nullable: false),
                    created_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    store_scope_key = table.Column<int>(type: "int", nullable: false, computedColumnSql: "ISNULL([store_id], 0)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_parameter", x => x.id);
                    table.ForeignKey(
                        name: "FK_system_parameter_app_user_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_system_parameter_store_store_id",
                        column: x => x.store_id,
                        principalTable: "store",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_parameter_created_by_user_id",
                table: "system_parameter",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_system_parameter_key_scope_store_effective",
                table: "system_parameter",
                columns: new[] { "parameter_key", "scope", "store_scope_key", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_parameter_key_scope_store_version",
                table: "system_parameter",
                columns: new[] { "parameter_key", "scope", "store_scope_key", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_parameter_store_id",
                table: "system_parameter",
                column: "store_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_parameter");
        }
    }
}
