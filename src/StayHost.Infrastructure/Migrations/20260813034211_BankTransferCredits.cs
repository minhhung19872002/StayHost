using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BankTransferCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_credits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Verdict = table.Column<int>(type: "integer", nullable: false),
                    MatchedReference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Expected = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_credits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_credits_BankReference",
                table: "bank_credits",
                column: "BankReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bank_credits_ResolvedAt_ImportedAt",
                table: "bank_credits",
                columns: new[] { "ResolvedAt", "ImportedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_credits");
        }
    }
}
