using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StayHost.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ServiceCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CertificateExpiresOn",
                table: "service_offerings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateName",
                table: "service_offerings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CertificateReminderSentOn",
                table: "service_offerings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenByExpiredCertificate",
                table: "service_offerings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CertificateExpiresOn",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "CertificateName",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "CertificateReminderSentOn",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "HiddenByExpiredCertificate",
                table: "service_offerings");
        }
    }
}
