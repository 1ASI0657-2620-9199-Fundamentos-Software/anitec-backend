using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    correlation_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    actor_user_id = table.Column<int>(type: "int", nullable: false),
                    actor_role = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    owner_user_id = table.Column<int>(type: "int", nullable: false),
                    action = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    entity_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    entity_id = table.Column<int>(type: "int", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    result = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    details_json = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_audit_log_entries", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_audit_log_entries_correlation_id",
                table: "audit_log_entries",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "i_x_audit_log_entries_owner_user_id_occurred_at",
                table: "audit_log_entries",
                columns: new[] { "owner_user_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");
        }
    }
}
