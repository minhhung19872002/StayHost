using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CouponsAndRedemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "CreditUsed",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<decimal>(
                name: "CouponDiscount",
                table: "bookings",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CouponId",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "coupons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Campaign = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    MaxDiscount = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    MinBookingTotal = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxRedemptions = table.Column<int>(type: "integer", nullable: true),
                    MaxPerUser = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "coupon_redemptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CouponId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Voided = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupon_redemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_coupon_redemptions_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_coupon_redemptions_coupons_CouponId",
                        column: x => x.CouponId,
                        principalTable: "coupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_coupon_redemptions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_CouponId",
                table: "bookings",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_redemptions_BookingId",
                table: "coupon_redemptions",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_redemptions_CouponId_UserId_Voided",
                table: "coupon_redemptions",
                columns: new[] { "CouponId", "UserId", "Voided" });

            migrationBuilder.CreateIndex(
                name: "IX_coupon_redemptions_CouponId_Voided",
                table: "coupon_redemptions",
                columns: new[] { "CouponId", "Voided" });

            migrationBuilder.CreateIndex(
                name: "IX_coupon_redemptions_UserId",
                table: "coupon_redemptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_coupons_Code",
                table: "coupons",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_coupons_CouponId",
                table: "bookings",
                column: "CouponId",
                principalTable: "coupons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_coupons_CouponId",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "coupon_redemptions");

            migrationBuilder.DropTable(
                name: "coupons");

            migrationBuilder.DropIndex(
                name: "IX_bookings_CouponId",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CouponDiscount",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "CouponId",
                table: "bookings");

            migrationBuilder.AlterColumn<decimal>(
                name: "CreditUsed",
                table: "bookings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);
        }
    }
}
