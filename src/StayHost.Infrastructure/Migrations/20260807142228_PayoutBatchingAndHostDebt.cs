using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PayoutBatchingAndHostDebt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayoutReference",
                table: "payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OwedToPlatform",
                table: "hosts",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutReference",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "OwedToPlatform",
                table: "hosts");
        }
    }
}
