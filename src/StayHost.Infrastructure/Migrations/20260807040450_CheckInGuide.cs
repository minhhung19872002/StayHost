using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CheckInGuide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine",
                table: "listings",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplianceNotes",
                table: "listings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckInFrom",
                table: "listings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<int>(
                name: "CheckInMethod",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckInTo",
                table: "listings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<TimeOnly>(
                name: "CheckOutBefore",
                table: "listings",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "Directions",
                table: "listings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoorCode",
                table: "listings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HostPhone",
                table: "listings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WifiName",
                table: "listings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WifiPassword",
                table: "listings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddressLine",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "ApplianceNotes",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "CheckInFrom",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "CheckInMethod",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "CheckInTo",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "CheckOutBefore",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "Directions",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "DoorCode",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "HostPhone",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "WifiName",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "WifiPassword",
                table: "listings");
        }
    }
}
