using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PayoutExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidOutAt",
                table: "payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutAttempts",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PayoutHoldReason",
                table: "payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PayoutLastAttemptOn",
                table: "payments",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidOutAt",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PayoutAttempts",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PayoutHoldReason",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PayoutLastAttemptOn",
                table: "payments");
        }
    }
}
