using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BadgeReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FavoriteReviewedOn",
                table: "listings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "SuperhostReviewedOn",
                table: "hosts",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FavoriteReviewedOn",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "SuperhostReviewedOn",
                table: "hosts");
        }
    }
}
