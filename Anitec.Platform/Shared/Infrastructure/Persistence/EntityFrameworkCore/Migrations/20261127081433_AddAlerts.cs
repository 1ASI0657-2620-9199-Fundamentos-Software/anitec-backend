using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    source_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    source_id = table.Column<int>(type: "int", nullable: false),
                    source_due_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                    animal_id = table.Column<int>(type: "int", nullable: true),
                    due_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    resolution_reason = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    resolved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    resolved_by_user_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    notified_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    notification_attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_alerts", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "i_x_alerts_owner_id_status_due_date",
                table: "alerts",
                columns: new[] { "owner_id", "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "i_x_alerts_source_type_source_id_source_due_date",
                table: "alerts",
                columns: new[] { "source_type", "source_id", "source_due_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");
        }
    }
}
