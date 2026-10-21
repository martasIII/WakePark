using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "paid_at_utc",
                schema: "billing",
                table: "orders",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "paid_by_bank_transaction_id",
                schema: "billing",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bank_transactions",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    booked_on = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    variable_symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    counterparty_account = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    counterparty_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    message = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_transactions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bank_transactions_enterprise_id_external_id",
                schema: "billing",
                table: "bank_transactions",
                columns: new[] { "enterprise_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bank_transactions_enterprise_id_status",
                schema: "billing",
                table: "bank_transactions",
                columns: new[] { "enterprise_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_transactions_order_id",
                schema: "billing",
                table: "bank_transactions",
                column: "order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_transactions",
                schema: "billing");

            migrationBuilder.DropColumn(
                name: "paid_at_utc",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "paid_by_bank_transaction_id",
                schema: "billing",
                table: "orders");
        }
    }
}
