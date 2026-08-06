using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ServicesMarketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceBookingId",
                table: "ledger_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_offerings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HostId = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Pricing = table.Column<int>(type: "integer", nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    MinQuantity = table.Column<int>(type: "integer", nullable: false),
                    MaxQuantity = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    TravelsToGuest = table.Column<bool>(type: "boolean", nullable: false),
                    ServiceRadiusKm = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    OpensAtHour = table.Column<int>(type: "integer", nullable: false),
                    ClosesAtHour = table.Column<int>(type: "integer", nullable: false),
                    IsPartner = table.Column<bool>(type: "boolean", nullable: false),
                    PartnerName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CommissionRate = table.Column<decimal>(type: "numeric(6,4)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Rating = table.Column<double>(type: "double precision", nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    SearchText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_offerings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_offerings_hosts_HostId",
                        column: x => x.HostId,
                        principalTable: "hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OfferingId = table.Column<int>(type: "integer", nullable: false),
                    GuestUserId = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PlatformCut = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ProviderPayout = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_bookings_service_offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "service_offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_bookings_users_GuestUserId",
                        column: x => x.GuestUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OfferingId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_images_service_offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "service_offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_ServiceBookingId",
                table: "ledger_entries",
                column: "ServiceBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_service_bookings_GuestUserId",
                table: "service_bookings",
                column: "GuestUserId");

            migrationBuilder.CreateIndex(
                name: "IX_service_bookings_OfferingId_StartsAt",
                table: "service_bookings",
                columns: new[] { "OfferingId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_service_bookings_Reference",
                table: "service_bookings",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_images_OfferingId",
                table: "service_images",
                column: "OfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_service_offerings_HostId",
                table: "service_offerings",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_service_offerings_Slug",
                table: "service_offerings",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_service_bookings_ServiceBookingId",
                table: "ledger_entries",
                column: "ServiceBookingId",
                principalTable: "service_bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_service_bookings_ServiceBookingId",
                table: "ledger_entries");

            migrationBuilder.DropTable(
                name: "service_bookings");

            migrationBuilder.DropTable(
                name: "service_images");

            migrationBuilder.DropTable(
                name: "service_offerings");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_ServiceBookingId",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "ServiceBookingId",
                table: "ledger_entries");
        }
    }
}
