using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WishlistVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wishlist_votes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WishlistId = table.Column<int>(type: "integer", nullable: false),
                    ListingId = table.Column<int>(type: "integer", nullable: false),
                    VoterKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Up = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlist_votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wishlist_votes_wishlists_WishlistId",
                        column: x => x.WishlistId,
                        principalTable: "wishlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_votes_WishlistId_ListingId",
                table: "wishlist_votes",
                columns: new[] { "WishlistId", "ListingId" });

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_votes_WishlistId_ListingId_VoterKey",
                table: "wishlist_votes",
                columns: new[] { "WishlistId", "ListingId", "VoterKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wishlist_votes");
        }
    }
}
