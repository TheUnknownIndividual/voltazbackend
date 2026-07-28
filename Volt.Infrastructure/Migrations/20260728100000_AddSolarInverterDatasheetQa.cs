using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260728100000_AddSolarInverterDatasheetQa")]
public partial class AddSolarInverterDatasheetQa : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "QaStatus",
            table: "SolarInverterSpecifications",
            type: "nvarchar(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "not-confirmed");
        migrationBuilder.AddColumn<string>(
            name: "QaNotes",
            table: "SolarInverterSpecifications",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "CorrectedExtractedText",
            table: "SolarInverterSpecifications",
            type: "nvarchar(max)",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "QaReviewedAt",
            table: "SolarInverterSpecifications",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "QaReviewedByAdminId",
            table: "SolarInverterSpecifications",
            type: "int",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "QaDoneAt",
            table: "SolarInverterSpecifications",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "ProductionPromotedAt",
            table: "SolarInverterSpecifications",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ProductionPromotionMessage",
            table: "SolarInverterSpecifications",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_SolarInverterSpecifications_QaStatus_QaDoneAt",
            table: "SolarInverterSpecifications",
            columns: new[] { "QaStatus", "QaDoneAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_SolarInverterSpecifications_QaStatus_QaDoneAt",
            table: "SolarInverterSpecifications");
        migrationBuilder.DropColumn(name: "QaStatus", table: "SolarInverterSpecifications");
        migrationBuilder.DropColumn(name: "QaNotes", table: "SolarInverterSpecifications");
        migrationBuilder.DropColumn(name: "CorrectedExtractedText", table: "SolarInverterSpecifications");
        migrationBuilder.DropColumn(name: "QaReviewedAt", table: "SolarInverterSpecifications");
        migrationBuilder.DropColumn(name: "QaReviewedByAdminId", table: "SolarInverterSpecifications");
        migrationBuilder.DropColumn(name: "QaDoneAt", table: "SolarInverterSpecifications");
        migrationBuilder.DropColumn(name: "ProductionPromotedAt", table: "SolarInverterSpecifications");
        migrationBuilder.DropColumn(name: "ProductionPromotionMessage", table: "SolarInverterSpecifications");
    }
}
