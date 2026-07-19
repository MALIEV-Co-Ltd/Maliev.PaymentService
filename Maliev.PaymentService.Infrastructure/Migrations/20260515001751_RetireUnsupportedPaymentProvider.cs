using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PaymentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetireUnsupportedPaymentProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE provider_configurations
                SET is_active = FALSE,
                    updated_at = NOW()
                WHERE payment_provider_id IN (
                    SELECT id
                    FROM payment_providers
                    WHERE LOWER(name) = 'paypal'
                );

                UPDATE payment_providers
                SET status = 'Disabled',
                    deleted_at = COALESCE(deleted_at, NOW()),
                    updated_at = NOW()
                WHERE LOWER(name) = 'paypal'
                  AND deleted_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE payment_providers
                SET status = 'Active',
                    deleted_at = NULL,
                    updated_at = NOW()
                WHERE LOWER(name) = 'paypal';

                UPDATE provider_configurations
                SET is_active = TRUE,
                    updated_at = NOW()
                WHERE payment_provider_id IN (
                    SELECT id
                    FROM payment_providers
                    WHERE LOWER(name) = 'paypal'
                );
                """);
        }
    }
}
