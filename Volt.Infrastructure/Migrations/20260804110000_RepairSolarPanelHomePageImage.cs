using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260804110000_RepairSolarPanelHomePageImage")]
public partial class RepairSolarPanelHomePageImage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE category
            SET HomePageProductId = replacement.Id
            FROM ProductCategories category
            CROSS APPLY (
                SELECT TOP (1) product.Id
                FROM Products product
                WHERE product.ProductCategoryId = category.Id
                  AND product.IsActive = 1
                  AND EXISTS (
                      SELECT 1
                      FROM ProductImages image
                      WHERE image.ProductId = product.Id AND image.Type = 1)
                ORDER BY product.Id DESC
            ) replacement
            WHERE category.IsActive = 1
              AND category.ShowOnHomePage = 1
              AND EXISTS (
                  SELECT 1
                  FROM ProductCategoryLanguages language
                  WHERE language.ProductCategoryId = category.Id
                    AND language.LanguageCode = 1
                    AND language.CategoryName IN (N'Gunes panel', N'Günəş paneli'))
              AND category.HomePageProductId <> replacement.Id;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Preserve any image selection subsequently made by an administrator.
    }
}
