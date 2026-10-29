using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anitec.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "owner_id",
                table: "profiles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_profiles_owner_id",
                table: "profiles",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_profiles_owner_id",
                table: "profiles");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "profiles");
        }
    }
}
