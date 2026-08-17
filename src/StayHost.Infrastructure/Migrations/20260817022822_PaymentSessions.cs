using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_sessions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderRef = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttemptKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Partial = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProviderTxnId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ResponseCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SettledBy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_sessions_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_AttemptKey",
                table: "payment_sessions",
                column: "AttemptKey");

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_BookingId",
                table: "payment_sessions",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_OrderRef",
                table: "payment_sessions",
                column: "OrderRef",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_sessions_Status_CreatedAt",
                table: "payment_sessions",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_sessions");
        }
    }
}
