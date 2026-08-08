using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserAdminHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SevereReviewedAt",
                table: "sanctions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SevereReviewedByUserId",
                table: "sanctions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HiddenBySanctionAt",
                table: "listings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LinkExpiresAt",
                table: "data_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkToken",
                table: "data_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "auth_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "auth_sessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SevereReviewedAt",
                table: "sanctions");

            migrationBuilder.DropColumn(
                name: "SevereReviewedByUserId",
                table: "sanctions");

            migrationBuilder.DropColumn(
                name: "HiddenBySanctionAt",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "LinkExpiresAt",
                table: "data_requests");

            migrationBuilder.DropColumn(
                name: "LinkToken",
                table: "data_requests");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "auth_sessions");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "auth_sessions");
        }
    }
}
