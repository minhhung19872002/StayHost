using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PricingAndCancellationTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationPolicy",
                table: "listings");

            migrationBuilder.AddColumn<int>(
                name: "CancellationTier",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WeekendSurchargeRate",
                table: "listings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CancellationTier",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Tax",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationTier",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "WeekendSurchargeRate",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "CancellationTier",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Tax",
                table: "bookings");

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicy",
                table: "listings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
