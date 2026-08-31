using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsViewedByAdminToRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AdminViewedAt",
                table: "ServiceRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsViewedByAdmin",
                table: "ServiceRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminViewedAt",
                table: "PartnershipRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsViewedByAdmin",
                table: "PartnershipRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AdminViewedAt",
                table: "Applications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsViewedByAdmin",
                table: "Applications",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminViewedAt",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "IsViewedByAdmin",
                table: "ServiceRequests");

            migrationBuilder.DropColumn(
                name: "AdminViewedAt",
                table: "PartnershipRequests");

            migrationBuilder.DropColumn(
                name: "IsViewedByAdmin",
                table: "PartnershipRequests");

            migrationBuilder.DropColumn(
                name: "AdminViewedAt",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IsViewedByAdmin",
                table: "Applications");
        }
    }
}
