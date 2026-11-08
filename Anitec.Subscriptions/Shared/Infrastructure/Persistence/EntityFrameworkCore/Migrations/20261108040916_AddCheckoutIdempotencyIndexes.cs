using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anitec.Subscriptions.Shared.Infrastructure.Persistence.EntityFrameworkCore.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutIdempotencyIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "i_x_subscriptions_stripe_subscription_id",
                table: "subscriptions",
                column: "stripe_subscription_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_payments_provider_payment_id",
                table: "payments",
                column: "provider_payment_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_subscriptions_stripe_subscription_id",
                table: "subscriptions");

            migrationBuilder.DropIndex(
                name: "i_x_payments_provider_payment_id",
                table: "payments");
        }
    }
}
