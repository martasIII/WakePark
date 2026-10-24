using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogBackoffice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_price_items_price_list_id_product_id",
                schema: "catalog",
                table: "price_items");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "catalog",
                table: "products",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb");

            migrationBuilder.AddColumn<string>(
                name: "badge",
                schema: "catalog",
                table: "products",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "channels",
                schema: "catalog",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "description",
                schema: "catalog",
                table: "product_categories",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                schema: "catalog",
                table: "product_categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                schema: "catalog",
                table: "price_lists",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "valid_from",
                schema: "catalog",
                table: "price_items",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Existující produkty jsou z e-shopu (ADR 0006) — ukazují se všude, ať po nasazení
            // nezmizí z webu. Ceny platí od data ceníku, do kterého patří.
            migrationBuilder.Sql("UPDATE catalog.products SET channels = 7;");
            migrationBuilder.Sql(
                "UPDATE catalog.price_items AS i SET valid_from = l.valid_from FROM catalog.price_lists AS l WHERE i.price_list_id = l.id;");

            migrationBuilder.CreateIndex(
                name: "ix_product_categories_parent_id",
                schema: "catalog",
                table: "product_categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_price_items_price_list_id_product_id_valid_from",
                schema: "catalog",
                table: "price_items",
                columns: new[] { "price_list_id", "product_id", "valid_from" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_product_categories_product_categories_parent_id",
                schema: "catalog",
                table: "product_categories",
                column: "parent_id",
                principalSchema: "catalog",
                principalTable: "product_categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_product_categories_product_categories_parent_id",
                schema: "catalog",
                table: "product_categories");

            migrationBuilder.DropIndex(
                name: "ix_product_categories_parent_id",
                schema: "catalog",
                table: "product_categories");

            migrationBuilder.DropIndex(
                name: "ix_price_items_price_list_id_product_id_valid_from",
                schema: "catalog",
                table: "price_items");

            migrationBuilder.DropColumn(
                name: "badge",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "channels",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "description",
                schema: "catalog",
                table: "product_categories");

            migrationBuilder.DropColumn(
                name: "parent_id",
                schema: "catalog",
                table: "product_categories");

            migrationBuilder.DropColumn(
                name: "title",
                schema: "catalog",
                table: "price_lists");

            migrationBuilder.DropColumn(
                name: "valid_from",
                schema: "catalog",
                table: "price_items");

            migrationBuilder.AlterColumn<string>(
                name: "description",
                schema: "catalog",
                table: "products",
                type: "jsonb",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_items_price_list_id_product_id",
                schema: "catalog",
                table: "price_items",
                columns: new[] { "price_list_id", "product_id" },
                unique: true);
        }
    }
}
