using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PartialPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BalanceAttempts",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceDue",
                table: "bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BalanceDueOn",
                table: "bookings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BalanceFirstFailedAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BalanceLastAttemptAt",
                table: "bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BalanceStatus",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DepositPaid",
                table: "bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalanceAttempts",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BalanceDue",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BalanceDueOn",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BalanceFirstFailedAt",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BalanceLastAttemptAt",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "BalanceStatus",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "DepositPaid",
                table: "bookings");
        }
    }
}
