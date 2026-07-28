using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SolarInverterSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolarInverterSpecifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    TechnicalPower = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModelLabel = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    SystemType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Phase = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NominalAcKw = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MaxDcKw = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    MpptCount = table.Column<int>(type: "int", nullable: true),
                    InputCount = table.Column<int>(type: "int", nullable: true),
                    MpptRange = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MaxDcVoltage = table.Column<int>(type: "int", nullable: true),
                    MaxInputCurrent = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WarrantyYears = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    IsEligible = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolarInverterSpecifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolarInverterSpecifications_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SolarInverterSpecifications_ProductId_TechnicalPower",
                table: "SolarInverterSpecifications",
                columns: new[] { "ProductId", "TechnicalPower" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolarInverterSpecifications_SystemType_Phase_IsEligible",
                table: "SolarInverterSpecifications",
                columns: new[] { "SystemType", "Phase", "IsEligible" });

            migrationBuilder.Sql(@"
WITH source_rows AS
(
    SELECT DISTINCT
        p.Id AS ProductId,
        p.ProductName AS ModelLabel,
        p.ProductSubCategoryId,
        LTRIM(RTRIM(pp.TechnicalPower)) AS TechnicalPower
    FROM Products p
    INNER JOIN ProductParametrs pp ON pp.ProductId = p.Id
    WHERE p.IsActive = 1
      AND pp.IsActive = 1
      AND p.ProductCategoryId = 2
      AND p.ProductBrandId = 2
      AND p.ProductSubCategoryId IN (10, 11, 12)
      AND NULLIF(LTRIM(RTRIM(pp.TechnicalPower)), '') IS NOT NULL
), parsed_rows AS
(
    SELECT
        source_rows.*,
        TRY_CONVERT(decimal(18, 3), REPLACE(REPLACE(LOWER(TechnicalPower), 'kw', ''), 'w', ''))
            * CASE WHEN LOWER(TechnicalPower) LIKE '%kw%' THEN 1.0 ELSE 0.001 END AS NominalAcKw
    FROM source_rows
)
INSERT INTO SolarInverterSpecifications
(
    ProductId, TechnicalPower, ModelLabel, SystemType, Phase, NominalAcKw, MaxDcKw,
    MpptCount, InputCount, MpptRange, MaxDcVoltage, MaxInputCurrent, WarrantyYears, IsEligible
)
SELECT
    ProductId,
    TechnicalPower,
    ModelLabel,
    CASE ProductSubCategoryId
        WHEN 10 THEN 'on-grid'
        WHEN 11 THEN 'off-grid'
        ELSE 'hybrid'
    END,
    CASE
        WHEN ProductSubCategoryId = 10 AND ProductId BETWEEN 241 AND 248 THEN 'single'
        WHEN ProductSubCategoryId = 11 AND ProductId = 300 THEN 'three'
        WHEN ProductSubCategoryId = 12 AND (ProductId BETWEEN 274 AND 286 OR ProductId = 278) THEN 'three'
        ELSE 'single'
    END,
    NominalAcKw,
    NominalAcKw * CASE
        WHEN ProductId IN (273, 279, 284, 285, 286) THEN 2.0
        WHEN ProductId IN (276, 277) THEN 1.6
        WHEN ProductId IN (288, 290, 291, 294, 295) THEN 1.2
        ELSE 1.5
    END,
    CASE
        WHEN ProductId IN (241, 242) THEN 1
        WHEN ProductId IN (257, 258, 259) THEN 3
        WHEN ProductId = 260 THEN 6
        WHEN ProductId IN (261, 262) THEN 10
        WHEN ProductId IN (263, 264) THEN 8
        WHEN ProductId = 266 THEN 6
        WHEN ProductId IN (269, 270) THEN 3
        WHEN ProductId IN (281, 282, 283, 284, 285) THEN 2
        ELSE 2
    END,
    CASE
        WHEN ProductId IN (241, 242) THEN 1
        WHEN ProductId BETWEEN 243 AND 256 THEN 4
        WHEN ProductId IN (257, 258, 259) THEN 6
        WHEN ProductId = 260 THEN 12
        WHEN ProductId IN (261, 262) THEN 20
        WHEN ProductId IN (263, 264) THEN 16
        WHEN ProductId = 266 THEN 12
        ELSE NULL
    END,
    CASE
        WHEN ProductId IN (241, 242) THEN '50-550 V'
        WHEN ProductId IN (243, 244, 245, 246) THEN '80-550 V'
        WHEN ProductId IN (250, 251, 252) THEN '140-1000 V'
        ELSE NULL
    END,
    CASE
        WHEN ProductId IN (241, 242, 243, 244, 245, 246) THEN 550
        WHEN ProductId IN (250, 251, 252, 253, 254, 255, 256) THEN 1100
        ELSE NULL
    END,
    CASE
        WHEN ProductId IN (252, 253, 254) THEN '20 A / MPPT'
        ELSE NULL
    END,
    5,
    1
FROM parsed_rows
WHERE NominalAcKw > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SolarInverterSpecifications");
        }
    }
}
