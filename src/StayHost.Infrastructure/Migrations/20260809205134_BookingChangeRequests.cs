using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BookingChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_change_requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    NewCheckIn = table.Column<DateOnly>(type: "date", nullable: false),
                    NewCheckOut = table.Column<DateOnly>(type: "date", nullable: false),
                    NewGuests = table.Column<int>(type: "integer", nullable: false),
                    NewAdults = table.Column<int>(type: "integer", nullable: false),
                    NewChildren = table.Column<int>(type: "integer", nullable: false),
                    NewInfants = table.Column<int>(type: "integer", nullable: false),
                    NewPets = table.Column<int>(type: "integer", nullable: false),
                    NewTotal = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ByHost = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_change_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_change_requests_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_change_requests_BookingId_Status",
                table: "booking_change_requests",
                columns: new[] { "BookingId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_change_requests");
        }
    }
}
