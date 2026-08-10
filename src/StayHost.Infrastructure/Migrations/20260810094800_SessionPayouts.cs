using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionPayouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidOutAt",
                table: "service_bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutReference",
                table: "service_bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutStatus",
                table: "service_bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaidOutAt",
                table: "experience_bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutReference",
                table: "experience_bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutStatus",
                table: "experience_bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidOutAt",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "PayoutReference",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "PayoutStatus",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "PaidOutAt",
                table: "experience_bookings");

            migrationBuilder.DropColumn(
                name: "PayoutReference",
                table: "experience_bookings");

            migrationBuilder.DropColumn(
                name: "PayoutStatus",
                table: "experience_bookings");
        }
    }
}
