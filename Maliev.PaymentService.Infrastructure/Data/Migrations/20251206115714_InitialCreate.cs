using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PaymentService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    supported_currencies = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    credentials = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    customer_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    order_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    payment_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_transaction_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payment_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    return_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    cancel_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    metadata = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    provider_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "'\\x00'::bytea")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_payment_providers_payment_provider_id",
                        column: x => x.payment_provider_id,
                        principalTable: "payment_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "provider_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    api_base_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_configurations", x => x.id);
                    table.ForeignKey(
                        name: "FK_provider_configurations_payment_providers_payment_provider_~",
                        column: x => x.payment_provider_id,
                        principalTable: "payment_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "'\\x00'::bytea")
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

            migrationBuilder.CreateTable(
                name: "webhook_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    refund_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    raw_payload = table.Column<string>(type: "text", nullable: false),
                    parsed_payload = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    signature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    signature_validated = table.Column<bool>(type: "boolean", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    processing_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    processing_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    processed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    next_retry_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "'\\x00'::bytea")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_events", x => x.id);
                    table.CheckConstraint("chk_webhook_events_attempts", "processing_attempts >= 0");
                    table.CheckConstraint("chk_webhook_events_processing_status", "processing_status IN ('Pending', 'Processing', 'Completed', 'Failed', 'Duplicate')");
                    table.ForeignKey(
                        name: "fk_webhook_events_payment_providers",
                        column: x => x.provider_id,
                        principalTable: "payment_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_webhook_events_payment_transactions",
                        column: x => x.payment_transaction_id,
                        principalTable: "payment_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transaction_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    new_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    provider_response = table.Column<string>(type: "jsonb", nullable: true),
                    error_details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefundTransactionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_transaction_logs_payment_transactions_payment_transaction_id",
                        column: x => x.payment_transaction_id,
                        principalTable: "payment_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transaction_logs_refund_transactions_RefundTransactionId",
                        column: x => x.RefundTransactionId,
                        principalTable: "refund_transactions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_providers_deleted_at",
                table: "payment_providers",
                column: "deleted_at");

            migrationBuilder.CreateIndex(
                name: "ix_payment_providers_name",
                table: "payment_providers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_providers_priority",
                table: "payment_providers",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "ix_payment_providers_status",
                table: "payment_providers",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_correlation_id",
                table: "payment_transactions",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_created_at",
                table: "payment_transactions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_customer_id",
                table: "payment_transactions",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_idempotency_key",
                table: "payment_transactions",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_order_id",
                table: "payment_transactions",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_payment_provider_id",
                table: "payment_transactions",
                column: "payment_provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_provider_transaction_id",
                table: "payment_transactions",
                column: "provider_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_payment_transactions_status",
                table: "payment_transactions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_provider_configurations_is_active",
                table: "provider_configurations",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_provider_configurations_payment_provider_id",
                table: "provider_configurations",
                column: "payment_provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_provider_configurations_provider_region",
                table: "provider_configurations",
                columns: new[] { "payment_provider_id", "region" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_transaction_logs_correlation_id",
                table: "transaction_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_logs_created_at",
                table: "transaction_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_logs_event_type",
                table: "transaction_logs",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_logs_payment_transaction_id",
                table: "transaction_logs",
                column: "payment_transaction_id");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_logs_RefundTransactionId",
                table: "transaction_logs",
                column: "RefundTransactionId");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_events_created_at",
                table: "webhook_events",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_webhook_events_next_retry",
                table: "webhook_events",
                column: "next_retry_at",
                filter: "next_retry_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_events_payment_id",
                table: "webhook_events",
                column: "payment_transaction_id",
                filter: "payment_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_webhook_events_processing_status",
                table: "webhook_events",
                columns: new[] { "processing_status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_webhook_events_refund_id",
                table: "webhook_events",
                column: "refund_transaction_id",
                filter: "refund_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uk_webhook_events_provider_event",
                table: "webhook_events",
                columns: new[] { "provider_id", "provider_event_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_configurations");

            migrationBuilder.DropTable(
                name: "transaction_logs");

            migrationBuilder.DropTable(
                name: "webhook_events");

            migrationBuilder.DropTable(
                name: "refund_transactions");

            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "payment_providers");
        }
    }
}
