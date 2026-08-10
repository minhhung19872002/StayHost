using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExperienceAttendanceAndReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AttendanceMarkedAt",
                table: "experience_bookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Attended",
                table: "experience_bookings",
                type: "boolean",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "experience_reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    ExperienceId = table.Column<int>(type: "integer", nullable: false),
                    AuthorUserId = table.Column<int>(type: "integer", nullable: false),
                    HostScore = table.Column<int>(type: "integer", nullable: false),
                    AsDescribedScore = table.Column<int>(type: "integer", nullable: false),
                    SafetyScore = table.Column<int>(type: "integer", nullable: false),
                    ValueScore = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experience_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_experience_reviews_experience_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "experience_bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_experience_reviews_experiences_ExperienceId",
                        column: x => x.ExperienceId,
                        principalTable: "experiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_experience_reviews_users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_experience_reviews_AuthorUserId",
                table: "experience_reviews",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_experience_reviews_BookingId",
                table: "experience_reviews",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_experience_reviews_ExperienceId",
                table: "experience_reviews",
                column: "ExperienceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experience_reviews");

            migrationBuilder.DropColumn(
                name: "AttendanceMarkedAt",
                table: "experience_bookings");

            migrationBuilder.DropColumn(
                name: "Attended",
                table: "experience_bookings");
        }
    }
}
