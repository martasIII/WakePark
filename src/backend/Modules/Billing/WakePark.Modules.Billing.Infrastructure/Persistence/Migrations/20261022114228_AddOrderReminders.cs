using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cancelled_at_utc",
                schema: "billing",
                table: "orders",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "payment_reminder_at_utc",
                schema: "billing",
                table: "orders",
                type: "timestamptz",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancelled_at_utc",
                schema: "billing",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "payment_reminder_at_utc",
                schema: "billing",
                table: "orders");
        }
    }
}
