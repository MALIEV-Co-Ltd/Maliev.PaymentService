using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PaymentService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
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
                name: "webhook_events");
        }
    }
}
