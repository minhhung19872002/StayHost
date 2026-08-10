using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ServiceOptionsAndSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CertificateName",
                table: "service_offerings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BufferMinutes",
                table: "service_offerings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxJobsPerDay",
                table: "service_offerings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxTravelKm",
                table: "service_offerings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OnSiteRequirements",
                table: "service_offerings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "TravelFeePerKm",
                table: "service_offerings",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "WorkingDaysMask",
                table: "service_offerings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "AddOnsTotal",
                table: "service_bookings",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ConditionsConfirmed",
                table: "service_bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "TravelFee",
                table: "service_bookings",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "service_add_ons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OfferingId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_add_ons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_add_ons_service_offerings_OfferingId",
                        column: x => x.OfferingId,
                        principalTable: "service_offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_booking_add_ons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    AddOnId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_booking_add_ons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_booking_add_ons_service_add_ons_AddOnId",
                        column: x => x.AddOnId,
                        principalTable: "service_add_ons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_service_booking_add_ons_service_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "service_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_add_ons_OfferingId",
                table: "service_add_ons",
                column: "OfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_service_booking_add_ons_AddOnId",
                table: "service_booking_add_ons",
                column: "AddOnId");

            migrationBuilder.CreateIndex(
                name: "IX_service_booking_add_ons_BookingId",
                table: "service_booking_add_ons",
                column: "BookingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_booking_add_ons");

            migrationBuilder.DropTable(
                name: "service_add_ons");

            migrationBuilder.DropColumn(
                name: "BufferMinutes",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "MaxJobsPerDay",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "MaxTravelKm",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "OnSiteRequirements",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "TravelFeePerKm",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "WorkingDaysMask",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "AddOnsTotal",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "ConditionsConfirmed",
                table: "service_bookings");

            migrationBuilder.DropColumn(
                name: "TravelFee",
                table: "service_bookings");

            migrationBuilder.AlterColumn<string>(
                name: "CertificateName",
                table: "service_offerings",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
