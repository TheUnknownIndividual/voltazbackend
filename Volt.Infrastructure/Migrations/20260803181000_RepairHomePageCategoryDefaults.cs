using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260803181000_RepairHomePageCategoryDefaults")]
public partial class RepairHomePageCategoryDefaults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SeedCategory(migrationBuilder, new[] { "Inverters", "İnvertorlar" }, 1, 249);
        SeedCategory(migrationBuilder, new[] { "Gunes panel", "Günəş paneli" }, 2, 420);
        SeedCategory(migrationBuilder, new[] { "Kabel və naqillər" }, 3, 37);
        SeedCategory(migrationBuilder, new[] { "Elektrik sistemləri" }, 4, 52);
        SeedCategory(migrationBuilder, new[] { "Fotovoltaik sistemlərin mühafizəsi" }, 5, 176);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE ProductCategories
            SET ShowOnHomePage = 0,
                HomePageDisplayOrder = 0,
                HomePageProductId = NULL
            WHERE EXISTS (
                SELECT 1
                FROM ProductCategoryLanguages language
                WHERE language.ProductCategoryId = ProductCategories.Id
                  AND language.LanguageCode = 1
                  AND language.CategoryName IN (
                      N'Inverters', N'İnvertorlar', N'Gunes panel', N'Günəş paneli',
                      N'Kabel və naqillər', N'Elektrik sistemləri',
                      N'Fotovoltaik sistemlərin mühafizəsi'));
            """);
    }

    private static void SeedCategory(
        MigrationBuilder migrationBuilder,
        IReadOnlyCollection<string> categoryNames,
        int displayOrder,
        int preferredProductId)
    {
        var names = string.Join(", ", categoryNames.Select(name => $"N'{name.Replace("'", "''")}'"));
        migrationBuilder.Sql($$"""
            UPDATE category
            SET ShowOnHomePage = 1,
                HomePageDisplayOrder = {{displayOrder}},
                HomePageProductId = COALESCE(
                    (
                        SELECT TOP (1) product.Id
                        FROM Products product
                        WHERE product.Id = {{preferredProductId}}
                          AND product.ProductCategoryId = category.Id
                          AND product.IsActive = 1
                          AND EXISTS (
                              SELECT 1 FROM ProductImages image
                              WHERE image.ProductId = product.Id AND image.Type = 1)
                    ),
                    (
                        SELECT TOP (1) product.Id
                        FROM Products product
                        WHERE product.ProductCategoryId = category.Id
                          AND product.IsActive = 1
                          AND EXISTS (
                              SELECT 1 FROM ProductImages image
                              WHERE image.ProductId = product.Id AND image.Type = 1)
                        ORDER BY product.Id
                    ))
            FROM ProductCategories category
            WHERE category.IsActive = 1
              AND EXISTS (
                  SELECT 1
                  FROM ProductCategoryLanguages language
                  WHERE language.ProductCategoryId = category.Id
                    AND language.LanguageCode = 1
                    AND language.CategoryName IN ({{names}}));
            """);
    }
}
