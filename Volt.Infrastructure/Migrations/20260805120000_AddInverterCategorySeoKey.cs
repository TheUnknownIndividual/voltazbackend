using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260805120000_AddInverterCategorySeoKey")]
public partial class AddInverterCategorySeoKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE TOP (1) category
            SET SeoKey = N'inverters'
            FROM ProductCategories category
            WHERE category.IsActive = 1
              AND category.SeoKey IS NULL
              AND EXISTS (
                  SELECT 1
                  FROM ProductCategoryLanguages language
                  WHERE language.ProductCategoryId = category.Id
                    AND language.LanguageCode = 1
                    AND language.CategoryName IN (
                        N'İnvertorlar', N'Invertorlar',
                        N'İnverterlər', N'Inverterlər'));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE ProductCategories
            SET SeoKey = NULL
            WHERE SeoKey = N'inverters';
            """);
    }
}
