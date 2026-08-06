using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoneyEngineAndLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServiceFeeRate",
                table: "listings");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "price_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EarlyBirdDays",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EarlyBirdPercent",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraGuestFee",
                table: "listings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "FreeGuestThreshold",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastMinuteDays",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LastMinutePercent",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxPets",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyDiscountPercent",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PetFee",
                table: "listings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "PetFeePerNight",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PetsAllowed",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyDiscountPercent",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Adults",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CancelledBy",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Children",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DiscountPercent",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraGuestFee",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GoodwillCredit",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HostPayout",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HostServiceFee",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Infants",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PetFee",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Pets",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PriceLinesJson",
                table: "bookings",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Promotion",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomBeforeDiscount",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RoomDiscount",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    Account = table.Column<int>(type: "integer", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Memo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ledger_entries_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tax_rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Base = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_Account_CreatedAt",
                table: "ledger_entries",
                columns: new[] { "Account", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_BookingId",
                table: "ledger_entries",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_TransactionId",
                table: "ledger_entries",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_tax_rules_Country_City",
                table: "tax_rules",
                columns: new[] { "Country", "City" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "tax_rules");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "price_rules");

            migrationBuilder.DropColumn(
                name: "EarlyBirdDays",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "EarlyBirdPercent",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "ExtraGuestFee",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "FreeGuestThreshold",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "LastMinuteDays",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "LastMinutePercent",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "MaxPets",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "MonthlyDiscountPercent",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "PetFee",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "PetFeePerNight",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "PetsAllowed",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "WeeklyDiscountPercent",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "Adults",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Children",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "ExtraGuestFee",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "GoodwillCredit",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "HostPayout",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "HostServiceFee",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Infants",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "PetFee",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Pets",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "PriceLinesJson",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "Promotion",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "RoomBeforeDiscount",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "RoomDiscount",
                table: "bookings");

            migrationBuilder.AddColumn<decimal>(
                name: "ServiceFeeRate",
                table: "listings",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
