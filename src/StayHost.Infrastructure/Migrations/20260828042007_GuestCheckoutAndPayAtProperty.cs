using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GuestCheckoutAndPayAtProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsPayAtProperty",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CashCollectedAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PaidAtProperty",
                table: "bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptsPayAtProperty",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "CashCollectedAt",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "PaidAtProperty",
                table: "bookings");
        }
    }
}
