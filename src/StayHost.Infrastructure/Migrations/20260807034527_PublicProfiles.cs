using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PublicProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bio was unbounded text until docs/01 TK-04 gave it a length. Narrowing
            // the column fails outright on a row that is already longer, so any
            // such row is cut to the new limit first.
            migrationBuilder.Sql(
                "UPDATE users SET \"Bio\" = left(\"Bio\", 700) WHERE length(\"Bio\") > 700;");

            migrationBuilder.AlterColumn<string>(
                name: "Bio",
                table: "users",
                type: "character varying(700)",
                maxLength: 700,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Interests",
                table: "users",
                type: "character varying(492)",
                maxLength: 492,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Occupation",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpokenLanguages",
                table: "users",
                type: "character varying(328)",
                maxLength: 328,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Interests",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Occupation",
                table: "users");

            migrationBuilder.DropColumn(
                name: "SpokenLanguages",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "Bio",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(700)",
                oldMaxLength: 700,
                oldNullable: true);
        }
    }
}
