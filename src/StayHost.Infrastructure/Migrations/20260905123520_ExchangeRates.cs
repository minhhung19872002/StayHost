using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExchangeRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Symbol = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    RateFromVnd = table.Column<decimal>(type: "numeric(20,12)", precision: 20, scale: 12, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    FeedRate = table.Column<decimal>(type: "numeric(20,12)", precision: 20, scale: 12, nullable: true),
                    FeedFetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exchange_rates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "exchange_rates",
                columns: new[] { "Id", "Code", "FeedFetchedAt", "FeedRate", "IsActive", "Label", "RateFromVnd", "SortOrder", "Source", "Symbol", "UpdatedAt", "UpdatedByAdminId" },
                values: new object[,]
                {
                    { 1, "VND", null, null, true, "Việt Nam Đồng", 1m, 0, 0, "₫", new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 2, "USD", null, null, true, "US Dollar", 0.0000392m, 1, 0, "$", new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 3, "EUR", null, null, true, "Euro", 0.0000362m, 2, 0, "€", new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 4, "JPY", null, null, true, "Japanese Yen", 0.00596m, 3, 0, "¥", new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 5, "KRW", null, null, true, "South Korean Won", 0.0535m, 4, 0, "₩", new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 6, "SGD", null, null, true, "Singapore Dollar", 0.0000508m, 5, 0, "S$", new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 7, "AUD", null, null, true, "Australian Dollar", 0.0000602m, 6, 0, "A$", new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { 8, "GBP", null, null, true, "British Pound", 0.0000309m, 7, 0, "£", new DateTime(2026, 9, 5, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_exchange_rates_Code",
                table: "exchange_rates",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exchange_rates");
        }
    }
}
