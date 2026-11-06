using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineDedications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "dedications",
                schema: "billing",
                table: "order_lines",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dedications",
                schema: "billing",
                table: "order_lines");
        }
    }
}
