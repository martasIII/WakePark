using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WakePark.Modules.Operations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatingCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schedule_overrides",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "working_hours",
                schema: "operations");

            migrationBuilder.DropIndex(
                name: "ix_cables_wake_park_id",
                schema: "operations",
                table: "cables");

            migrationBuilder.AddColumn<string>(
                name: "code",
                schema: "operations",
                table: "cables",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "length_meters",
                schema: "operations",
                table: "cables",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "obstacles",
                schema: "operations",
                table: "cables",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            // Vleky založené před zavedením kódu (jen vývojová data, produkce žádné nemá) dostanou
            // kód z Id, aby prošel unikátní index. Výchozí hodnota obstacles musí být platný JSON.
            migrationBuilder.Sql("UPDATE operations.cables SET code = 'vlek-' || left(id::text, 8) WHERE code = '';");

            migrationBuilder.CreateTable(
                name: "blockings",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wake_park_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    end_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blockings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operating_days",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wake_park_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operating_days", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "park_settings",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wake_park_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    public_hours_days_ahead = table.Column<int>(type: "integer", nullable: false),
                    public_booking_days_ahead = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_park_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cables_wake_park_id_code",
                schema: "operations",
                table: "cables",
                columns: new[] { "wake_park_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_blockings_wake_park_id_start_utc_end_utc",
                schema: "operations",
                table: "blockings",
                columns: new[] { "wake_park_id", "start_utc", "end_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_operating_days_wake_park_id_date",
                schema: "operations",
                table: "operating_days",
                columns: new[] { "wake_park_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_park_settings_wake_park_id",
                schema: "operations",
                table: "park_settings",
                column: "wake_park_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blockings",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "operating_days",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "park_settings",
                schema: "operations");

            migrationBuilder.DropIndex(
                name: "ix_cables_wake_park_id_code",
                schema: "operations",
                table: "cables");

            migrationBuilder.DropColumn(
                name: "code",
                schema: "operations",
                table: "cables");

            migrationBuilder.DropColumn(
                name: "length_meters",
                schema: "operations",
                table: "cables");

            migrationBuilder.DropColumn(
                name: "obstacles",
                schema: "operations",
                table: "cables");

            migrationBuilder.CreateTable(
                name: "schedule_overrides",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    custom_end_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    custom_start_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_overrides", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "working_hours",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_working_hours", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cables_wake_park_id",
                schema: "operations",
                table: "cables",
                column: "wake_park_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_overrides_cable_id_date",
                schema: "operations",
                table: "schedule_overrides",
                columns: new[] { "cable_id", "date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_working_hours_cable_id_season_id_day_of_week",
                schema: "operations",
                table: "working_hours",
                columns: new[] { "cable_id", "season_id", "day_of_week" });
        }
    }
}
