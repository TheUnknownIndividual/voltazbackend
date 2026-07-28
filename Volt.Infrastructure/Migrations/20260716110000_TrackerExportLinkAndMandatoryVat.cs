using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260716110000_TrackerExportLinkAndMandatoryVat")]
    public partial class TrackerExportLinkAndMandatoryVat : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdminTrackedProjectId",
                table: "SolarCalculationLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolarCalculationLogs_AdminTrackedProjectId",
                table: "SolarCalculationLogs",
                column: "AdminTrackedProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_SolarCalculationLogs_AdminTrackedProjects_AdminTrackedProjectId",
                table: "SolarCalculationLogs",
                column: "AdminTrackedProjectId",
                principalTable: "AdminTrackedProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AlterColumn<bool>(
                name: "IncludesAdv",
                table: "AdminTrackedProjects",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            // Existing tracker prices are stored as the pre-ƏDV subtotal.
            migrationBuilder.Sql(@"
UPDATE AdminTrackedProjects
SET OfferPrice = ROUND(OfferPrice * 1.18, 2),
    IncludesAdv = CAST(1 AS bit);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE AdminTrackedProjects
SET OfferPrice = ROUND(OfferPrice / 1.18, 2),
    IncludesAdv = CAST(0 AS bit);");

            migrationBuilder.DropForeignKey(
                name: "FK_SolarCalculationLogs_AdminTrackedProjects_AdminTrackedProjectId",
                table: "SolarCalculationLogs");

            migrationBuilder.DropIndex(
                name: "IX_SolarCalculationLogs_AdminTrackedProjectId",
                table: "SolarCalculationLogs");

            migrationBuilder.DropColumn(
                name: "AdminTrackedProjectId",
                table: "SolarCalculationLogs");

            migrationBuilder.AlterColumn<bool>(
                name: "IncludesAdv",
                table: "AdminTrackedProjects",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);
        }
    }
}
