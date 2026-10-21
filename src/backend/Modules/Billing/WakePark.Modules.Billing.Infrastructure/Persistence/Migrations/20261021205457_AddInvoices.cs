using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "invoice_number_series",
                schema: "billing",
                columns: table => new
                {
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    last_sequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_number_series", x => new { x.enterprise_id, x.year });
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    issued_on = table.Column<DateOnly>(type: "date", nullable: false),
                    taxable_supply_on = table.Column<DateOnly>(type: "date", nullable: false),
                    variable_symbol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    seller_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    seller_company_id = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    seller_vat_id = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    seller_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    seller_registration = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    seller_contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    seller_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    buyer_company_id = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    buyer_vat_id = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    buyer_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    buyer_registration = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    buyer_contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    buyer_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    total_net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                schema: "billing",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoice_lines_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "billing",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invoice_lines_invoice_id",
                schema: "billing",
                table: "invoice_lines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_enterprise_id_number",
                schema: "billing",
                table: "invoices",
                columns: new[] { "enterprise_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_order_id",
                schema: "billing",
                table: "invoices",
                column: "order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoice_lines",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "invoice_number_series",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "billing");
        }
    }
}
