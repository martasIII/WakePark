using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tenancy");

            migrationBuilder.CreateTable(
                name: "enterprises",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_house_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    address_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    registration_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vat_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bank_account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    owner_user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enterprises", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "wake_parks",
                schema: "tenancy",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enterprise_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address_house_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    address_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    reservation_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wake_parks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_enterprises_registration_number",
                schema: "tenancy",
                table: "enterprises",
                column: "registration_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wake_parks_enterprise_id",
                schema: "tenancy",
                table: "wake_parks",
                column: "enterprise_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "enterprises",
                schema: "tenancy");

            migrationBuilder.DropTable(
                name: "wake_parks",
                schema: "tenancy");
        }
    }
}
