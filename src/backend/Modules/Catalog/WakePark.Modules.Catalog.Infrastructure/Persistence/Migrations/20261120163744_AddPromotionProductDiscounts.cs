using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionProductDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "product_discounts",
                schema: "catalog",
                table: "promotions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "product_discounts",
                schema: "catalog",
                table: "promotions");
        }
    }
}
