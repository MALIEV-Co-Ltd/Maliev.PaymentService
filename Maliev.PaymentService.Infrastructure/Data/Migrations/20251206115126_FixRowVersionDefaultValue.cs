using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PaymentService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixRowVersionDefaultValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add row_version columns with default value (columns may not exist in older databases)
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "webhook_events",
                type: "bytea",
                nullable: false,
                defaultValueSql: "'\\x00'::bytea");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "refund_transactions",
                type: "bytea",
                nullable: false,
                defaultValueSql: "'\\x00'::bytea");

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "payment_transactions",
                type: "bytea",
                nullable: false,
                defaultValueSql: "'\\x00'::bytea");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "refund_transactions");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "payment_transactions");
        }
    }
}
