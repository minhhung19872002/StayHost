using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ListingWizardAndLegalDeclarations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasDangerousAnimals",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSecurityCameras",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasWeaponsOnProperty",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplete",
                table: "listings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "listings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityCameraNote",
                table: "listings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WizardStep",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasDangerousAnimals",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "HasSecurityCameras",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "HasWeaponsOnProperty",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "IsComplete",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "SecurityCameraNote",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "WizardStep",
                table: "listings");
        }
    }
}
