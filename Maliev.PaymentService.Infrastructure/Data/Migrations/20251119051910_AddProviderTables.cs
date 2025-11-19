using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maliev.PaymentService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderTables : Migration
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_configurations");

            migrationBuilder.DropTable(
                name: "payment_providers");
        }
    }
}
