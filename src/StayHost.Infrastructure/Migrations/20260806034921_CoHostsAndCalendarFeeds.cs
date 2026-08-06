using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoHostsAndCalendarFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IcalToken",
                table: "listings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalUid",
                table: "calendar_blocks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeedId",
                table: "calendar_blocks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "calendar_feeds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListingId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_feeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_calendar_feeds_listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "co_hosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CoHostUserId = table.Column<int>(type: "integer", nullable: true),
                    ListingId = table.Column<int>(type: "integer", nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InviteToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_co_hosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_co_hosts_listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_co_hosts_users_CoHostUserId",
                        column: x => x.CoHostUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_co_hosts_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calendar_blocks_FeedId_ExternalUid",
                table: "calendar_blocks",
                columns: new[] { "FeedId", "ExternalUid" });

            migrationBuilder.CreateIndex(
                name: "IX_calendar_feeds_ListingId",
                table: "calendar_feeds",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_co_hosts_CoHostUserId",
                table: "co_hosts",
                column: "CoHostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_co_hosts_InviteToken",
                table: "co_hosts",
                column: "InviteToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_co_hosts_ListingId",
                table: "co_hosts",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_co_hosts_OwnerUserId_Email",
                table: "co_hosts",
                columns: new[] { "OwnerUserId", "Email" });

            migrationBuilder.AddForeignKey(
                name: "FK_calendar_blocks_calendar_feeds_FeedId",
                table: "calendar_blocks",
                column: "FeedId",
                principalTable: "calendar_feeds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_calendar_blocks_calendar_feeds_FeedId",
                table: "calendar_blocks");

            migrationBuilder.DropTable(
                name: "calendar_feeds");

            migrationBuilder.DropTable(
                name: "co_hosts");

            migrationBuilder.DropIndex(
                name: "IX_calendar_blocks_FeedId_ExternalUid",
                table: "calendar_blocks");

            migrationBuilder.DropColumn(
                name: "IcalToken",
                table: "listings");

            migrationBuilder.DropColumn(
                name: "ExternalUid",
                table: "calendar_blocks");

            migrationBuilder.DropColumn(
                name: "FeedId",
                table: "calendar_blocks");
        }
    }
}
