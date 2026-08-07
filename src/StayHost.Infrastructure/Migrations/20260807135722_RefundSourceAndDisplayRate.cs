using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefundSourceAndDisplayRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayCurrency",
                table: "bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DisplayRate",
                table: "bookings",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayCurrency",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "DisplayRate",
                table: "bookings");
        }
    }
}
