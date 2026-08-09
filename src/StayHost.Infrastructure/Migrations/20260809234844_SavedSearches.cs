using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SavedSearches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "saved_searches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Q = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    MinPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    MaxPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Guests = table.Column<int>(type: "integer", nullable: false),
                    AmenitiesCsv = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    RoomType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Bedrooms = table.Column<int>(type: "integer", nullable: false),
                    SuperhostOnly = table.Column<bool>(type: "boolean", nullable: false),
                    InstantBookOnly = table.Column<bool>(type: "boolean", nullable: false),
                    HostLanguagesCsv = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastNotifiedListingId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_searches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_searches_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_saved_searches_UserId",
                table: "saved_searches",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_searches");
        }
    }
}
