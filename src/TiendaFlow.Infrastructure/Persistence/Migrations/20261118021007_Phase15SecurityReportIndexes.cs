using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TiendaFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase15SecurityReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_audit_log_logged_at",
                table: "audit_log",
                column: "logged_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_log_logged_at",
                table: "audit_log");
        }
    }
}
