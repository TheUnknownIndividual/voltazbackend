using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

/// <summary>
/// Stores each imported PDF once. Wattage variants reference its SHA-256 in
/// DatasheetContentJson, keeping the calculator read path schema-compatible.
/// </summary>
[DbContext(typeof(DataContext))]
[Migration("20260727173000_AddSolarInverterDatasheetDocuments")]
public partial class AddSolarInverterDatasheetDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SolarInverterDatasheetDocuments",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Sha256 = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                ParserVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                DocumentKind = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                PageCount = table.Column<int>(type: "int", nullable: false),
                RequiresOcr = table.Column<bool>(type: "bit", nullable: false),
                ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ParsedContentJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SolarInverterDatasheetDocuments", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SolarInverterDatasheetDocuments_Sha256",
            table: "SolarInverterDatasheetDocuments",
            column: "Sha256",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SolarInverterDatasheetDocuments_SourceUrl",
            table: "SolarInverterDatasheetDocuments",
            column: "SourceUrl");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "SolarInverterDatasheetDocuments");
    }
}
