using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Vouchers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherDedication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dedication",
                schema: "vouchers",
                table: "vouchers",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dedication",
                schema: "vouchers",
                table: "vouchers");
        }
    }
}
