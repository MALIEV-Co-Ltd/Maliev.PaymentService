using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PaymentService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RefundTransactionId",
                table: "transaction_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "refund_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_refund_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    refund_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    provider_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    internal_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    initiated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refund_transactions", x => x.id);
                    table.CheckConstraint("chk_refund_transactions_amount_positive", "amount > 0");
                    table.CheckConstraint("chk_refund_transactions_currency_length", "LENGTH(currency) = 3");
                    table.CheckConstraint("chk_refund_transactions_status", "status IN ('pending', 'processing', 'completed', 'failed')");
                    table.CheckConstraint("chk_refund_transactions_type", "refund_type IN ('full', 'partial')");
                    table.ForeignKey(
                        name: "fk_refund_transactions_payment_providers",
                        column: x => x.provider_id,
                        principalTable: "payment_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refund_transactions_payment_transactions",
                        column: x => x.payment_transaction_id,
                        principalTable: "payment_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transaction_logs_RefundTransactionId",
                table: "transaction_logs",
                column: "RefundTransactionId");

            migrationBuilder.CreateIndex(
                name: "idx_refund_transactions_correlation_id",
                table: "refund_transactions",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "idx_refund_transactions_payment_id",
                table: "refund_transactions",
                columns: new[] { "payment_transaction_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_refund_transactions_provider_id",
                table: "refund_transactions",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "idx_refund_transactions_status",
                table: "refund_transactions",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uk_refund_transactions_idempotency_key",
                table: "refund_transactions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_transaction_logs_refund_transactions_RefundTransactionId",
                table: "transaction_logs",
                column: "RefundTransactionId",
                principalTable: "refund_transactions",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transaction_logs_refund_transactions_RefundTransactionId",
                table: "transaction_logs");

            migrationBuilder.DropTable(
                name: "refund_transactions");

            migrationBuilder.DropIndex(
                name: "IX_transaction_logs_RefundTransactionId",
                table: "transaction_logs");

            migrationBuilder.DropColumn(
                name: "RefundTransactionId",
                table: "transaction_logs");
        }
    }
}
