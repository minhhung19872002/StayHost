using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GiftCardAwaitsPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "payment_sessions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "GiftCardId",
                table: "payment_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_GiftCardId",
                table: "payment_sessions",
                column: "GiftCardId");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_sessions_gift_cards_GiftCardId",
                table: "payment_sessions",
                column: "GiftCardId",
                principalTable: "gift_cards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_sessions_gift_cards_GiftCardId",
                table: "payment_sessions");

            migrationBuilder.DropIndex(
                name: "IX_payment_sessions_GiftCardId",
                table: "payment_sessions");

            migrationBuilder.DropColumn(
                name: "GiftCardId",
                table: "payment_sessions");

            migrationBuilder.AlterColumn<int>(
                name: "BookingId",
                table: "payment_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
