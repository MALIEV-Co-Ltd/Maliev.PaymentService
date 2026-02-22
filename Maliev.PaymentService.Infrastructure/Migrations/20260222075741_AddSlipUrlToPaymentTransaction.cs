using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PaymentService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSlipUrlToPaymentTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slip_bank_name",
                table: "payment_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "slip_extracted_amount",
                table: "payment_transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slip_transfer_date",
                table: "payment_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slip_url",
                table: "payment_transactions",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slip_verification_notes",
                table: "payment_transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "slip_verified_at",
                table: "payment_transactions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "slip_bank_name",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "slip_extracted_amount",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "slip_transfer_date",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "slip_url",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "slip_verification_notes",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "slip_verified_at",
                table: "payment_transactions");
        }
    }
}
