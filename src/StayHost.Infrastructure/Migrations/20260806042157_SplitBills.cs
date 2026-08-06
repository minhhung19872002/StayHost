using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bill_splits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    OrganiserUserId = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_splits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bill_splits_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bill_splits_users_OrganiserUserId",
                        column: x => x.OrganiserUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bill_shares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SplitId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CardLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bill_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bill_shares_bill_splits_SplitId",
                        column: x => x.SplitId,
                        principalTable: "bill_splits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bill_shares_SplitId",
                table: "bill_shares",
                column: "SplitId");

            migrationBuilder.CreateIndex(
                name: "IX_bill_shares_Token",
                table: "bill_shares",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bill_splits_BookingId",
                table: "bill_splits",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bill_splits_OrganiserUserId",
                table: "bill_splits",
                column: "OrganiserUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bill_shares");

            migrationBuilder.DropTable(
                name: "bill_splits");
        }
    }
}
