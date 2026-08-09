using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <summary>
    /// docs/01 AT-02 — reports grow from listings only to listings, people,
    /// messages and reviews.
    ///
    /// Scaffolding this produced a DropTable followed by a CreateTable, which
    /// would have emptied the moderation queue on the deploy that shipped it. The
    /// rows are real work somebody is waiting on, so the table is renamed and
    /// widened in place instead. Every row that already exists is a listing
    /// report, and ReportTarget.Listing is 0, so the new column needs no backfill
    /// beyond its default — which is then dropped, because the model does not
    /// declare one and a lingering database default drifts from the snapshot.
    /// </summary>
    public partial class AbuseReportsAcrossSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "listing_reports", newName: "abuse_reports");

            // Renaming a table leaves its constraints carrying the old name.
            migrationBuilder.Sql("""
                ALTER TABLE abuse_reports RENAME CONSTRAINT "PK_listing_reports" TO "PK_abuse_reports";
                ALTER TABLE abuse_reports RENAME CONSTRAINT "FK_listing_reports_listings_ListingId" TO "FK_abuse_reports_listings_ListingId";
                ALTER TABLE abuse_reports RENAME CONSTRAINT "FK_listing_reports_users_ReporterUserId" TO "FK_abuse_reports_users_ReporterUserId";
                """);

            migrationBuilder.RenameIndex(
                name: "IX_listing_reports_ListingId", table: "abuse_reports",
                newName: "IX_abuse_reports_ListingId");
            migrationBuilder.RenameIndex(
                name: "IX_listing_reports_ReporterUserId", table: "abuse_reports",
                newName: "IX_abuse_reports_ReporterUserId");
            migrationBuilder.RenameIndex(
                name: "IX_listing_reports_Status", table: "abuse_reports",
                newName: "IX_abuse_reports_Status");

            // A row about a person carries no listing, so the column stops being required.
            migrationBuilder.AlterColumn<int>(
                name: "ListingId", table: "abuse_reports", type: "integer", nullable: true,
                oldClrType: typeof(int), oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Target", table: "abuse_reports", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.Sql("""ALTER TABLE abuse_reports ALTER COLUMN "Target" DROP DEFAULT;""");

            migrationBuilder.AddColumn<int>(
                name: "ReportedUserId", table: "abuse_reports", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "MessageId", table: "abuse_reports", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(
                name: "ReviewId", table: "abuse_reports", type: "integer", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_abuse_reports_Target", table: "abuse_reports", column: "Target");
            migrationBuilder.CreateIndex(
                name: "IX_abuse_reports_ReportedUserId", table: "abuse_reports", column: "ReportedUserId");
            migrationBuilder.CreateIndex(
                name: "IX_abuse_reports_MessageId", table: "abuse_reports", column: "MessageId");
            migrationBuilder.CreateIndex(
                name: "IX_abuse_reports_ReviewId", table: "abuse_reports", column: "ReviewId");

            migrationBuilder.AddForeignKey(
                name: "FK_abuse_reports_users_ReportedUserId", table: "abuse_reports",
                column: "ReportedUserId", principalTable: "users", principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_abuse_reports_messages_MessageId", table: "abuse_reports",
                column: "MessageId", principalTable: "messages", principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_abuse_reports_reviews_ReviewId", table: "abuse_reports",
                column: "ReviewId", principalTable: "reviews", principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reports about people, messages and reviews have nowhere to go in the
            // old shape; they are dropped rather than rewritten into listing
            // reports they are not.
            migrationBuilder.Sql("DELETE FROM abuse_reports WHERE \"Target\" <> 0;");

            migrationBuilder.DropForeignKey(name: "FK_abuse_reports_reviews_ReviewId", table: "abuse_reports");
            migrationBuilder.DropForeignKey(name: "FK_abuse_reports_messages_MessageId", table: "abuse_reports");
            migrationBuilder.DropForeignKey(name: "FK_abuse_reports_users_ReportedUserId", table: "abuse_reports");

            migrationBuilder.DropIndex(name: "IX_abuse_reports_ReviewId", table: "abuse_reports");
            migrationBuilder.DropIndex(name: "IX_abuse_reports_MessageId", table: "abuse_reports");
            migrationBuilder.DropIndex(name: "IX_abuse_reports_ReportedUserId", table: "abuse_reports");
            migrationBuilder.DropIndex(name: "IX_abuse_reports_Target", table: "abuse_reports");

            migrationBuilder.DropColumn(name: "ReviewId", table: "abuse_reports");
            migrationBuilder.DropColumn(name: "MessageId", table: "abuse_reports");
            migrationBuilder.DropColumn(name: "ReportedUserId", table: "abuse_reports");
            migrationBuilder.DropColumn(name: "Target", table: "abuse_reports");

            migrationBuilder.AlterColumn<int>(
                name: "ListingId", table: "abuse_reports", type: "integer", nullable: false,
                defaultValue: 0, oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.RenameIndex(
                name: "IX_abuse_reports_Status", table: "abuse_reports",
                newName: "IX_listing_reports_Status");
            migrationBuilder.RenameIndex(
                name: "IX_abuse_reports_ReporterUserId", table: "abuse_reports",
                newName: "IX_listing_reports_ReporterUserId");
            migrationBuilder.RenameIndex(
                name: "IX_abuse_reports_ListingId", table: "abuse_reports",
                newName: "IX_listing_reports_ListingId");

            migrationBuilder.Sql("""
                ALTER TABLE abuse_reports RENAME CONSTRAINT "FK_abuse_reports_users_ReporterUserId" TO "FK_listing_reports_users_ReporterUserId";
                ALTER TABLE abuse_reports RENAME CONSTRAINT "FK_abuse_reports_listings_ListingId" TO "FK_listing_reports_listings_ListingId";
                ALTER TABLE abuse_reports RENAME CONSTRAINT "PK_abuse_reports" TO "PK_listing_reports";
                """);

            migrationBuilder.RenameTable(name: "abuse_reports", newName: "listing_reports");
        }
    }
}
