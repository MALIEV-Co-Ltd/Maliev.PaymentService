using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PaymentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixTransactionLogMappingBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transaction_logs_payment_transactions_payment_transaction_id",
                table: "transaction_logs");

            migrationBuilder.AddForeignKey(
                name: "fk_transaction_logs_payment_transactions_payment_transaction_id",
                table: "transaction_logs",
                column: "payment_transaction_id",
                principalTable: "payment_transactions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transaction_logs_payment_transactions_payment_transaction_id",
                table: "transaction_logs");

            migrationBuilder.AddForeignKey(
                name: "fk_transaction_logs_payment_transactions_payment_transaction_id",
                table: "transaction_logs",
                column: "payment_transaction_id",
                principalTable: "payment_transactions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
