using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations;

public partial class SolarInverterPhaseRepair : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
UPDATE specification
SET
    SystemType = CASE
        WHEN LOWER(product.ProductName) LIKE '%on grid%' THEN 'on-grid'
        WHEN LOWER(product.ProductName) LIKE '%off grid%' THEN 'off-grid'
        WHEN LOWER(product.ProductName) LIKE '%hybird%'
          OR LOWER(product.ProductName) LIKE '%hybrid%' THEN 'hybrid'
        ELSE specification.SystemType
    END,
    Phase = CASE
        WHEN LOWER(product.ProductName) LIKE '%tl3%'
          OR LOWER(product.ProductName) LIKE '%mod %'
          OR LOWER(product.ProductName) LIKE '%max %'
          OR LOWER(product.ProductName) LIKE '%wit %'
        THEN 'three'
        ELSE 'single'
    END
FROM SolarInverterSpecifications specification
INNER JOIN Products product ON product.Id = specification.ProductId
WHERE LOWER(product.ProductName) LIKE 'growatt%'
  AND LOWER(product.ProductName) NOT LIKE '%micro%';");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The previous ID-based phase values cannot be reconstructed safely
        // after a name-based repair, so this additive correction has no rollback SQL.
    }
}
