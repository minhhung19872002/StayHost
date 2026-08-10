using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExperienceVetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsChildren",
                table: "experiences",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "experiences",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyPhone",
                table: "experiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InsuranceExpiresOn",
                table: "experiences",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsurancePolicy",
                table: "experiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LicenceExpiresOn",
                table: "experiences",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenceName",
                table: "experiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModerationStatus",
                table: "experiences",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "experiences",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "experiences",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerNote",
                table: "experiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyPlan",
                table: "experiences",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedForReviewAt",
                table: "experiences",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowsChildren",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "EmergencyPhone",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "InsuranceExpiresOn",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "InsurancePolicy",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "LicenceExpiresOn",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "LicenceName",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "ReviewerNote",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "SafetyPlan",
                table: "experiences");

            migrationBuilder.DropColumn(
                name: "SubmittedForReviewAt",
                table: "experiences");
        }
    }
}
