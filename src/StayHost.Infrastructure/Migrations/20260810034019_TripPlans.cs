using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TripPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trip_plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_plans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_plans_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_itinerary_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripPlanId = table.Column<int>(type: "integer", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AddedByUserId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_itinerary_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_itinerary_items_trip_plans_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "trip_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_plan_bookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripPlanId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_plan_bookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_plan_bookings_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trip_plan_bookings_trip_plans_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "trip_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trip_plan_members",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TripPlanId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_plan_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_plan_members_trip_plans_TripPlanId",
                        column: x => x.TripPlanId,
                        principalTable: "trip_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trip_plan_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trip_itinerary_items_TripPlanId_Day_SortOrder",
                table: "trip_itinerary_items",
                columns: new[] { "TripPlanId", "Day", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_trip_plan_bookings_BookingId",
                table: "trip_plan_bookings",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_trip_plan_bookings_TripPlanId_BookingId",
                table: "trip_plan_bookings",
                columns: new[] { "TripPlanId", "BookingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_plan_members_TripPlanId_UserId",
                table: "trip_plan_members",
                columns: new[] { "TripPlanId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_plan_members_UserId",
                table: "trip_plan_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_trip_plans_OwnerId",
                table: "trip_plans",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trip_itinerary_items");

            migrationBuilder.DropTable(
                name: "trip_plan_bookings");

            migrationBuilder.DropTable(
                name: "trip_plan_members");

            migrationBuilder.DropTable(
                name: "trip_plans");
        }
    }
}
