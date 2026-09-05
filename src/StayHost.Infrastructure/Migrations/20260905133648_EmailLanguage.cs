using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EmailLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CtaUrl",
                table: "email_messages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "email_messages",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawBody",
                table: "email_messages",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawTitle",
                table: "email_messages",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TranslatedAt",
                table: "email_messages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CtaUrl",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "RawBody",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "RawTitle",
                table: "email_messages");

            migrationBuilder.DropColumn(
                name: "TranslatedAt",
                table: "email_messages");
        }
    }
}
