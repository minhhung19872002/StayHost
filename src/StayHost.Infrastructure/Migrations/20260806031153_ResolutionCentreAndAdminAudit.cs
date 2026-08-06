using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResolutionCentreAndAdminAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdminScope",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "admin_audit",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Target = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Before = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    After = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_audit_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resolution_cases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    OpenedByUserId = table.Column<int>(type: "integer", nullable: false),
                    OpenedByHost = table.Column<bool>(type: "boolean", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AmountClaimed = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EvidenceUrls = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ResponseDueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Response = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Decision = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AmountAwarded = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resolution_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resolution_cases_bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_resolution_cases_users_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_resolution_cases_users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resolution_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CaseId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resolution_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_resolution_events_resolution_cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "resolution_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_ActorUserId",
                table: "admin_audit",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_CreatedAt",
                table: "admin_audit",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_admin_audit_Target",
                table: "admin_audit",
                column: "Target");

            migrationBuilder.CreateIndex(
                name: "IX_resolution_cases_BookingId",
                table: "resolution_cases",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_resolution_cases_DecidedByUserId",
                table: "resolution_cases",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_resolution_cases_OpenedByUserId",
                table: "resolution_cases",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_resolution_cases_Reference",
                table: "resolution_cases",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_resolution_cases_Status",
                table: "resolution_cases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_resolution_events_CaseId_CreatedAt",
                table: "resolution_events",
                columns: new[] { "CaseId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_audit");

            migrationBuilder.DropTable(
                name: "resolution_events");

            migrationBuilder.DropTable(
                name: "resolution_cases");

            migrationBuilder.DropColumn(
                name: "AdminScope",
                table: "users");
        }
    }
}
