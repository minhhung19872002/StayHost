using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HostPayoutSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountLast4",
                table: "hosts",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountName",
                table: "hosts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBankName",
                table: "hosts",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutSchedule",
                table: "hosts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayoutAccountLast4",
                table: "hosts");

            migrationBuilder.DropColumn(
                name: "PayoutAccountName",
                table: "hosts");

            migrationBuilder.DropColumn(
                name: "PayoutBankName",
                table: "hosts");

            migrationBuilder.DropColumn(
                name: "PayoutSchedule",
                table: "hosts");
        }
    }
}
