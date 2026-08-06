using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BlindReviewsAndSystemMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reviews_ListingId",
                table: "reviews");

            migrationBuilder.AddColumn<DateTime>(
                name: "EditableUntil",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateNote",
                table: "reviews",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "guest_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ListingId_PublishedAt",
                table: "reviews",
                columns: new[] { "ListingId", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_reviews_ListingId_PublishedAt",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "EditableUntil",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "PrivateNote",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "guest_reviews");

            migrationBuilder.CreateIndex(
                name: "IX_reviews_ListingId",
                table: "reviews",
                column: "ListingId");
        }
    }
}
