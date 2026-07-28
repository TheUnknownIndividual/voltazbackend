using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

/// <summary>
/// Adds nullable, manufacturer-datasheet fields used by the PV design review.
/// Existing catalog entries deliberately remain preliminary until their exact
/// datasheet revision is reviewed and recorded.
/// </summary>
[DbContext(typeof(DataContext))]
[Migration("20260723120000_AddSolarInverterDatasheetEngineeringFields")]
public partial class AddSolarInverterDatasheetEngineeringFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Manufacturer", table: "SolarInverterSpecifications", type: "nvarchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "RegionalGridVersion", table: "SolarInverterSpecifications", type: "nvarchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "DatasheetUrl", table: "SolarInverterSpecifications", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<string>(name: "DatasheetRevision", table: "SolarInverterSpecifications", type: "nvarchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "DatasheetContentJson", table: "SolarInverterSpecifications", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "DatasheetReviewedAt", table: "SolarInverterSpecifications", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "MaxAcApparentPowerKva", table: "SolarInverterSpecifications", type: "decimal(18,3)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "MaxAcOutputCurrentA", table: "SolarInverterSpecifications", type: "decimal(18,3)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "NominalAcVoltageV", table: "SolarInverterSpecifications", type: "decimal(18,3)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "SupportedGridVoltageRange", table: "SolarInverterSpecifications", type: "nvarchar(120)", maxLength: 120, nullable: true);
        migrationBuilder.AddColumn<string>(name: "SupportedFrequencyRange", table: "SolarInverterSpecifications", type: "nvarchar(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<int>(name: "StartVoltageV", table: "SolarInverterSpecifications", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "MpptMinVoltageV", table: "SolarInverterSpecifications", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "MpptMaxVoltageV", table: "SolarInverterSpecifications", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "NominalDcVoltageV", table: "SolarInverterSpecifications", type: "int", nullable: true);
        migrationBuilder.AddColumn<int>(name: "StringInputsPerMppt", table: "SolarInverterSpecifications", type: "int", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "MaxOperatingCurrentPerStringA", table: "SolarInverterSpecifications", type: "decimal(18,3)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "MaxOperatingCurrentPerMpptA", table: "SolarInverterSpecifications", type: "decimal(18,3)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "MaxShortCircuitCurrentPerStringA", table: "SolarInverterSpecifications", type: "decimal(18,3)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "MaxShortCircuitCurrentPerMpptA", table: "SolarInverterSpecifications", type: "decimal(18,3)", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "HasIntegratedDcSwitch", table: "SolarInverterSpecifications", type: "bit", nullable: true);
        migrationBuilder.AddColumn<string>(name: "AcSpdClass", table: "SolarInverterSpecifications", type: "nvarchar(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<string>(name: "DcSpdClass", table: "SolarInverterSpecifications", type: "nvarchar(80)", maxLength: 80, nullable: true);
        migrationBuilder.AddColumn<bool>(name: "HasAfci", table: "SolarInverterSpecifications", type: "bit", nullable: true);
        migrationBuilder.AddColumn<string>(name: "RequiredGridCertifications", table: "SolarInverterSpecifications", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[]
        {
            "Manufacturer", "RegionalGridVersion", "DatasheetUrl", "DatasheetRevision", "DatasheetContentJson", "DatasheetReviewedAt",
            "MaxAcApparentPowerKva", "MaxAcOutputCurrentA", "NominalAcVoltageV", "SupportedGridVoltageRange", "SupportedFrequencyRange",
            "StartVoltageV", "MpptMinVoltageV", "MpptMaxVoltageV", "NominalDcVoltageV", "StringInputsPerMppt",
            "MaxOperatingCurrentPerStringA", "MaxOperatingCurrentPerMpptA", "MaxShortCircuitCurrentPerStringA", "MaxShortCircuitCurrentPerMpptA",
            "HasIntegratedDcSwitch", "AcSpdClass", "DcSpdClass", "HasAfci", "RequiredGridCertifications"
        })
        {
            migrationBuilder.DropColumn(name: column, table: "SolarInverterSpecifications");
        }
    }
}
