using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HotelsAndPriceMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // docs/01 MR-08 — a hotel sells rooms of a kind, so two bookings on
            // the same dates are normal as long as there are rooms left. The
            // exclusion constraint of docs/03 §2 still governs whole-place
            // listings; hotel rows carry a room type and are counted instead.
            migrationBuilder.Sql("""ALTER TABLE bookings DROP CONSTRAINT IF EXISTS bookings_no_overlap;""");

            migrationBuilder.AddColumn<int>(
                name: "RoomTypeId",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "price_match_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    GuestUserId = table.Column<int>(type: "integer", nullable: false),
                    CompetitorUrl = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CompetitorNightlyRate = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    OurNightlyRate = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_match_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_match_claims_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_price_match_claims_users_GuestUserId",
                        column: x => x.GuestUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListingId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Inventory = table.Column<int>(type: "integer", nullable: false),
                    MaxGuests = table.Column<int>(type: "integer", nullable: false),
                    Beds = table.Column<int>(type: "integer", nullable: false),
                    SizeSqm = table.Column<double>(type: "double precision", nullable: false),
                    PricePerNight = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    Features = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_room_types_listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_RoomTypeId",
                table: "bookings",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_price_match_claims_BookingId",
                table: "price_match_claims",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_price_match_claims_GuestUserId",
                table: "price_match_claims",
                column: "GuestUserId");

            migrationBuilder.CreateIndex(
                name: "IX_room_types_ListingId_SortOrder",
                table: "room_types",
                columns: new[] { "ListingId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_room_types_RoomTypeId",
                table: "bookings",
                column: "RoomTypeId",
                principalTable: "room_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                ALTER TABLE bookings
                ADD CONSTRAINT bookings_no_overlap
                EXCLUDE USING gist (
                    "ListingId" WITH =,
                    daterange("CheckIn", "CheckOut", '[)') WITH &&
                )
                WHERE ("Status" IN (1, 2, 3, 4) AND "RoomTypeId" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE bookings DROP CONSTRAINT IF EXISTS bookings_no_overlap;""");
            migrationBuilder.Sql("""
                ALTER TABLE bookings
                ADD CONSTRAINT bookings_no_overlap
                EXCLUDE USING gist (
                    "ListingId" WITH =,
                    daterange("CheckIn", "CheckOut", '[)') WITH &&
                )
                WHERE ("Status" IN (1, 2, 3, 4));
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_bookings_room_types_RoomTypeId",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "price_match_claims");

            migrationBuilder.DropTable(
                name: "room_types");

            migrationBuilder.DropIndex(
                name: "IX_bookings_RoomTypeId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "RoomTypeId",
                table: "bookings");
        }
    }
}
