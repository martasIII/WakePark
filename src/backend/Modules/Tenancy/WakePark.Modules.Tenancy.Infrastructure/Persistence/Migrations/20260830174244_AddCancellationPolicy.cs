using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Tenancy.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "cancellation_policy_cancellation_allowed",
                schema: "tenancy",
                table: "wake_parks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "cancellation_policy_free_cancellation_hours",
                schema: "tenancy",
                table: "wake_parks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_policy_late_cancellation_refund_percent",
                schema: "tenancy",
                table: "wake_parks",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "cancellation_policy_no_show_refund_percent",
                schema: "tenancy",
                table: "wake_parks",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_policy_cancellation_allowed",
                schema: "tenancy",
                table: "wake_parks");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_free_cancellation_hours",
                schema: "tenancy",
                table: "wake_parks");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_late_cancellation_refund_percent",
                schema: "tenancy",
                table: "wake_parks");

            migrationBuilder.DropColumn(
                name: "cancellation_policy_no_show_refund_percent",
                schema: "tenancy",
                table: "wake_parks");
        }
    }
}
