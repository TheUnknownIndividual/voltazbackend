using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations;

public partial class SolarInverterCatalogSeedFix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
WITH source_rows AS
(
    SELECT DISTINCT
        p.Id AS ProductId,
        p.ProductName AS ModelLabel,
        LTRIM(RTRIM(pp.TechnicalPower)) AS TechnicalPower
    FROM Products p
    INNER JOIN ProductParametrs pp ON pp.ProductId = p.Id
    WHERE p.IsActive = 1
      AND pp.IsActive = 1
      AND LOWER(p.ProductName) LIKE 'growatt%'
      AND LOWER(p.ProductName) NOT LIKE '%micro%'
      AND (
          LOWER(p.ProductName) LIKE '%on grid%'
          OR LOWER(p.ProductName) LIKE '%off grid%'
          OR LOWER(p.ProductName) LIKE '%hybird%'
          OR LOWER(p.ProductName) LIKE '%hybrid%'
      )
      AND NULLIF(LTRIM(RTRIM(pp.TechnicalPower)), '') IS NOT NULL
), parsed_rows AS
(
    SELECT
        source_rows.*,
        TRY_CONVERT(decimal(18, 3), REPLACE(REPLACE(LOWER(TechnicalPower), 'kw', ''), 'w', ''))
            * CASE WHEN LOWER(TechnicalPower) LIKE '%kw%' THEN 1.0 ELSE 0.001 END AS NominalAcKw
    FROM source_rows
), normalized_rows AS
(
    SELECT
        parsed_rows.*,
        CASE
            WHEN LOWER(ModelLabel) LIKE '%on grid%' THEN 'on-grid'
            WHEN LOWER(ModelLabel) LIKE '%off grid%' THEN 'off-grid'
            ELSE 'hybrid'
        END AS SystemType,
        CASE
            WHEN LOWER(ModelLabel) LIKE '%tl3%'
              OR LOWER(ModelLabel) LIKE '%mod %'
              OR LOWER(ModelLabel) LIKE '%max %'
              OR LOWER(ModelLabel) LIKE '%wit %'
            THEN 'three'
            ELSE 'single'
        END AS Phase
    FROM parsed_rows
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
    SystemType,
    Phase,
    NominalAcKw,
    NominalAcKw * CASE
        WHEN LOWER(ModelLabel) LIKE '%mod %' THEN 1.5
        WHEN LOWER(ModelLabel) LIKE '%max %' THEN 1.5
        ELSE 1.5
    END,
    CASE WHEN Phase = 'three' THEN 2 ELSE 2 END,
    CASE WHEN Phase = 'three' THEN 4 ELSE 2 END,
    NULL,
    NULL,
    NULL,
    5,
    1
FROM normalized_rows
WHERE NominalAcKw > 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM SolarInverterSpecifications existing
      WHERE existing.ProductId = normalized_rows.ProductId
        AND existing.TechnicalPower = normalized_rows.TechnicalPower
  );");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DELETE target
FROM SolarInverterSpecifications target
INNER JOIN Products p ON p.Id = target.ProductId
WHERE LOWER(p.ProductName) LIKE 'growatt%'
  AND LOWER(p.ProductName) NOT LIKE '%micro%'
  AND (
      LOWER(p.ProductName) LIKE '%on grid%'
      OR LOWER(p.ProductName) LIKE '%off grid%'
      OR LOWER(p.ProductName) LIKE '%hybird%'
      OR LOWER(p.ProductName) LIKE '%hybrid%'
  );");
    }
}
