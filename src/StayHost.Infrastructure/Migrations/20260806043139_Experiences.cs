using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Experiences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExperienceBookingId",
                table: "ledger_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "experiences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HostId = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    MaxGroup = table.Column<int>(type: "integer", nullable: false),
                    MinGuests = table.Column<int>(type: "integer", nullable: false),
                    Languages = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MinAge = table.Column<int>(type: "integer", nullable: false),
                    MeetingPoint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Included = table.Column<string>(type: "text", nullable: false),
                    PricePerPerson = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PrivateGroupPrice = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Rating = table.Column<double>(type: "double precision", nullable: false),
                    ReviewCount = table.Column<int>(type: "integer", nullable: false),
                    SearchText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experiences_hosts_HostId",
                        column: x => x.HostId,
                        principalTable: "hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "experience_images",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExperienceId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_images", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experience_images_experiences_ExperienceId",
                        column: x => x.ExperienceId,
                        principalTable: "experiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "experience_slots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExperienceId = table.Column<int>(type: "integer", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    SeatsTaken = table.Column<int>(type: "integer", nullable: false),
                    IsPrivate = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experience_slots_experiences_ExperienceId",
                        column: x => x.ExperienceId,
                        principalTable: "experiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "experience_bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SlotId = table.Column<int>(type: "integer", nullable: false),
                    GuestUserId = table.Column<int>(type: "integer", nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    IsPrivate = table.Column<bool>(type: "boolean", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ServiceFee = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    HostServiceFee = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    HostPayout = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CancelReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experience_bookings_experience_slots_SlotId",
                        column: x => x.SlotId,
                        principalTable: "experience_slots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_experience_bookings_users_GuestUserId",
                        column: x => x.GuestUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_entries_ExperienceBookingId",
                table: "ledger_entries",
                column: "ExperienceBookingId");

            migrationBuilder.CreateIndex(
                name: "IX_experience_bookings_GuestUserId",
                table: "experience_bookings",
                column: "GuestUserId");

            migrationBuilder.CreateIndex(
                name: "IX_experience_bookings_Reference",
                table: "experience_bookings",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_experience_bookings_SlotId",
                table: "experience_bookings",
                column: "SlotId");

            migrationBuilder.CreateIndex(
                name: "IX_experience_images_ExperienceId",
                table: "experience_images",
                column: "ExperienceId");

            migrationBuilder.CreateIndex(
                name: "IX_experience_slots_ExperienceId_StartsAt",
                table: "experience_slots",
                columns: new[] { "ExperienceId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_experiences_HostId",
                table: "experiences",
                column: "HostId");

            migrationBuilder.CreateIndex(
                name: "IX_experiences_Slug",
                table: "experiences",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ledger_entries_experience_bookings_ExperienceBookingId",
                table: "ledger_entries",
                column: "ExperienceBookingId",
                principalTable: "experience_bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ledger_entries_experience_bookings_ExperienceBookingId",
                table: "ledger_entries");

            migrationBuilder.DropTable(
                name: "experience_bookings");

            migrationBuilder.DropTable(
                name: "experience_images");

            migrationBuilder.DropTable(
                name: "experience_slots");

            migrationBuilder.DropTable(
                name: "experiences");

            migrationBuilder.DropIndex(
                name: "IX_ledger_entries_ExperienceBookingId",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "ExperienceBookingId",
                table: "ledger_entries");
        }
    }
}
