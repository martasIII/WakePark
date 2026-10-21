using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "order_number_series",
                schema: "billing",
                columns: table => new
                {
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_number_series", x => new { x.enterprise_id, x.year });
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<long>(type: "bigint", nullable: false),
                    access_token = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    customer_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    customer_company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    customer_company_id = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    customer_vat_id = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    language = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    total_gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    placed_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    payment_due_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    terms_accepted_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "order_lines",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    display_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    line_net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_lines_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "billing",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_lines_order_id",
                schema: "billing",
                table: "order_lines",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_lines_product_id",
                schema: "billing",
                table: "order_lines",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_access_token",
                schema: "billing",
                table: "orders",
                column: "access_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_enterprise_id_number",
                schema: "billing",
                table: "orders",
                columns: new[] { "enterprise_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_status_payment_due_at_utc",
                schema: "billing",
                table: "orders",
                columns: new[] { "status", "payment_due_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_lines",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "order_number_series",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "billing");
        }
    }
}
