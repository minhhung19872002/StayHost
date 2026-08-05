using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NamedWishlists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "favorites",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WishlistId",
                table: "favorites",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "wishlists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wishlists_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_favorites_WishlistId",
                table: "favorites",
                column: "WishlistId");

            migrationBuilder.CreateIndex(
                name: "IX_wishlists_SessionId",
                table: "wishlists",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_wishlists_UserId",
                table: "wishlists",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_favorites_wishlists_WishlistId",
                table: "favorites",
                column: "WishlistId",
                principalTable: "wishlists",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_favorites_wishlists_WishlistId",
                table: "favorites");

            migrationBuilder.DropTable(
                name: "wishlists");

            migrationBuilder.DropIndex(
                name: "IX_favorites_WishlistId",
                table: "favorites");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "favorites");

            migrationBuilder.DropColumn(
                name: "WishlistId",
                table: "favorites");
        }
    }
}
