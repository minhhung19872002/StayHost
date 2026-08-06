using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ThirdPartyLiability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThirdPartyContact",
                table: "shield_claims",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThirdPartyKind",
                table: "shield_claims",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThirdPartyName",
                table: "shield_claims",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThirdPartyContact",
                table: "shield_claims");

            migrationBuilder.DropColumn(
                name: "ThirdPartyKind",
                table: "shield_claims");

            migrationBuilder.DropColumn(
                name: "ThirdPartyName",
                table: "shield_claims");
        }
    }
}
