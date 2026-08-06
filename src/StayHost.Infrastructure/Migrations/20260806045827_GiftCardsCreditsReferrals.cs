using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GiftCardsCreditsReferrals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditUsed",
                table: "bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "credit_entries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Reason = table.Column<int>(type: "integer", nullable: false),
                    Memo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_entries_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_entries_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gift_cards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Remaining = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PurchasedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RecipientEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    RedeemedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gift_cards_users_PurchasedByUserId",
                        column: x => x.PurchasedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_gift_cards_users_RedeemedByUserId",
                        column: x => x.RedeemedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "referrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferrerUserId = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    InviteeEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InviteeUserId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReferrerReward = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    InviteeReward = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_referrals_users_InviteeUserId",
                        column: x => x.InviteeUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_referrals_users_ReferrerUserId",
                        column: x => x.ReferrerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_credit_entries_BookingId",
                table: "credit_entries",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_entries_UserId_CreatedAt",
                table: "credit_entries",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_gift_cards_Code",
                table: "gift_cards",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gift_cards_PurchasedByUserId",
                table: "gift_cards",
                column: "PurchasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_gift_cards_RedeemedByUserId",
                table: "gift_cards",
                column: "RedeemedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_referrals_Code",
                table: "referrals",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_referrals_InviteeUserId",
                table: "referrals",
                column: "InviteeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_referrals_ReferrerUserId_InviteeEmail",
                table: "referrals",
                columns: new[] { "ReferrerUserId", "InviteeEmail" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_entries");

            migrationBuilder.DropTable(
                name: "gift_cards");

            migrationBuilder.DropTable(
                name: "referrals");

            migrationBuilder.DropColumn(
                name: "CreditUsed",
                table: "bookings");
        }
    }
}
