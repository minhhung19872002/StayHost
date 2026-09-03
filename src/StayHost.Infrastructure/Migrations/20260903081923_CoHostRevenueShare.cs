using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoHostRevenueShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CoHostShare",
                table: "payments",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PayeeHostId",
                table: "co_hosts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PayoutFixed",
                table: "co_hosts",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PayoutKind",
                table: "co_hosts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PayoutPercent",
                table: "co_hosts",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutProposedAt",
                table: "co_hosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PayoutRespondedAt",
                table: "co_hosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutStatus",
                table: "co_hosts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "co_host_payouts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CoHostId = table.Column<int>(type: "integer", nullable: false),
                    PayeeHostId = table.Column<int>(type: "integer", nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Basis = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Fixed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Earnings = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PayoutReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Deducted = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClawedBack = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_co_host_payouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_co_host_payouts_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_co_host_payouts_co_hosts_CoHostId",
                        column: x => x.CoHostId,
                        principalTable: "co_hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_co_host_payouts_hosts_PayeeHostId",
                        column: x => x.PayeeHostId,
                        principalTable: "hosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_co_hosts_PayeeHostId",
                table: "co_hosts",
                column: "PayeeHostId");

            migrationBuilder.CreateIndex(
                name: "IX_co_host_payouts_BookingId",
                table: "co_host_payouts",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_co_host_payouts_CoHostId_BookingId",
                table: "co_host_payouts",
                columns: new[] { "CoHostId", "BookingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_co_host_payouts_PayeeHostId_Status",
                table: "co_host_payouts",
                columns: new[] { "PayeeHostId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_co_host_payouts_PayoutReference",
                table: "co_host_payouts",
                column: "PayoutReference");

            migrationBuilder.AddForeignKey(
                name: "FK_co_hosts_hosts_PayeeHostId",
                table: "co_hosts",
                column: "PayeeHostId",
                principalTable: "hosts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_co_hosts_hosts_PayeeHostId",
                table: "co_hosts");

            migrationBuilder.DropTable(
                name: "co_host_payouts");

            migrationBuilder.DropIndex(
                name: "IX_co_hosts_PayeeHostId",
                table: "co_hosts");

            migrationBuilder.DropColumn(
                name: "CoHostShare",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "PayeeHostId",
                table: "co_hosts");

            migrationBuilder.DropColumn(
                name: "PayoutFixed",
                table: "co_hosts");

            migrationBuilder.DropColumn(
                name: "PayoutKind",
                table: "co_hosts");

            migrationBuilder.DropColumn(
                name: "PayoutPercent",
                table: "co_hosts");

            migrationBuilder.DropColumn(
                name: "PayoutProposedAt",
                table: "co_hosts");

            migrationBuilder.DropColumn(
                name: "PayoutRespondedAt",
                table: "co_hosts");

            migrationBuilder.DropColumn(
                name: "PayoutStatus",
                table: "co_hosts");
        }
    }
}
