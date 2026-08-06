using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StayShield : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shield_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    OpenedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Side = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Claimed = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ExpensesClaimed = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    RehousingDifference = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Remedy = table.Column<int>(type: "integer", nullable: false),
                    Approved = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Deductible = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    CreditGranted = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    PaidFromFund = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    RecoveredFromCounterparty = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    RecoveredLater = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Decision = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Appealed = table.Column<bool>(type: "boolean", nullable: false),
                    AppealReviewerUserId = table.Column<int>(type: "integer", nullable: true),
                    NeedsManualReview = table.Column<bool>(type: "boolean", nullable: false),
                    RespondBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirstResponseDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecisionDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shield_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shield_claims_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shield_claims_users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_shield_claims_users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shield_events",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shield_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shield_events_shield_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "shield_claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shield_evidence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shield_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shield_evidence_shield_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "shield_claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shield_fund_movements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    ClaimId = table.Column<int>(type: "integer", nullable: true),
                    Memo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Period = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shield_fund_movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shield_fund_movements_shield_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "shield_claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "shield_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    DeclaredOnListing = table.Column<bool>(type: "boolean", nullable: false),
                    Allowed = table.Column<decimal>(type: "numeric(14,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shield_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shield_items_shield_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "shield_claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shield_claims_BookingId_Status",
                table: "shield_claims",
                columns: new[] { "BookingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_shield_claims_DecidedByUserId",
                table: "shield_claims",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shield_claims_OpenedByUserId",
                table: "shield_claims",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_shield_claims_Reference",
                table: "shield_claims",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shield_events_ClaimId_CreatedAt",
                table: "shield_events",
                columns: new[] { "ClaimId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_shield_evidence_ClaimId",
                table: "shield_evidence",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_shield_fund_movements_ClaimId",
                table: "shield_fund_movements",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_shield_fund_movements_Period_Kind",
                table: "shield_fund_movements",
                columns: new[] { "Period", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_shield_items_ClaimId",
                table: "shield_items",
                column: "ClaimId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shield_events");

            migrationBuilder.DropTable(
                name: "shield_evidence");

            migrationBuilder.DropTable(
                name: "shield_fund_movements");

            migrationBuilder.DropTable(
                name: "shield_items");

            migrationBuilder.DropTable(
                name: "shield_claims");
        }
    }
}
