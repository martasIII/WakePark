using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Vouchers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vouchers");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    processed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vouchers",
                schema: "vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<long>(type: "bigint", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    display_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    language = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    piece = table.Column<int>(type: "integer", nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: false),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vouchers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "vouchers",
                table: "outbox_messages",
                column: "occurred_at_utc",
                filter: "processed_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_enterprise_id_code",
                schema: "vouchers",
                table: "vouchers",
                columns: new[] { "enterprise_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vouchers_order_id_product_id_piece",
                schema: "vouchers",
                table: "vouchers",
                columns: new[] { "order_id", "product_id", "piece" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "vouchers");

            migrationBuilder.DropTable(
                name: "vouchers",
                schema: "vouchers");
        }
    }
}
