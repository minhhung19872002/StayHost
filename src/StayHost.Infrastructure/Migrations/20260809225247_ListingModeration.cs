using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ListingModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "listings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "listings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "listings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "listings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedForReviewAt",
                table: "listings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_listings_ReviewStatus",
                table: "listings",
                column: "ReviewStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_listings_ReviewStatus",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "SubmittedForReviewAt",
                table: "listings");
        }
    }
}
