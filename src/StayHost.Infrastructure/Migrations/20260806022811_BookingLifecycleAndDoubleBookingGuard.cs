using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookingLifecycleAndDoubleBookingGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_ListingId",
                table: "bookings");

            migrationBuilder.AddColumn<int>(
                name: "MinNights",
                table: "price_rules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AdvanceNoticeHours",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BlockedCheckInDays",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BlockedCheckOutDays",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CalendarVisibilityMonths",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxNights",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SameDayCutoffHour",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "listings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TurnoverDays",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldExpiresAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestExpiresAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "booking_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_events_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_listing_range",
                table: "bookings",
                columns: new[] { "ListingId", "CheckIn", "CheckOut" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_events_BookingId_CreatedAt",
                table: "booking_events",
                columns: new[] { "BookingId", "CreatedAt" });

            // docs/03 §2: "Một khoảng ngày chỉ được bán một lần… Đây là yêu cầu
            // bắt buộc, không phải tối ưu hoá." A check in application code loses
            // that race, so the guarantee is a database constraint. The half-open
            // range '[)' is what makes back-to-back stays legal: one guest's
            // check-out day is the next guest's check-in day.
            //
            // Statuses listed are the four that hold dates (BookingLifecycle
            // .BlocksDates): PendingPayment(1), Confirmed(2), InProgress(3),
            // Completed(4). A request awaiting host approval is deliberately
            // absent — docs/03 §2 says it must not hold the dates.
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS btree_gist;""");
            migrationBuilder.Sql("""
                ALTER TABLE bookings
                ADD CONSTRAINT bookings_no_overlap
                EXCLUDE USING gist (
                    "ListingId" WITH =,
                    daterange("CheckIn", "CheckOut", '[)') WITH &&
                )
                WHERE ("Status" IN (1, 2, 3, 4));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE bookings DROP CONSTRAINT IF EXISTS bookings_no_overlap;""");

            migrationBuilder.DropTable(
                name: "booking_events");

            migrationBuilder.DropIndex(
                name: "ix_bookings_listing_range",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "MinNights",
                table: "price_rules");

            migrationBuilder.DropColumn(
                name: "AdvanceNoticeHours",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "BlockedCheckInDays",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "BlockedCheckOutDays",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "CalendarVisibilityMonths",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "MaxNights",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "SameDayCutoffHour",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "TurnoverDays",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "HoldExpiresAt",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "RequestExpiresAt",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_ListingId",
                table: "bookings",
                column: "ListingId");
        }
    }
}
