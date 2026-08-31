using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260804130000_AddProductCategorySeoKey")]
public partial class AddProductCategorySeoKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SeoKey",
            table: "ProductCategories",
            type: "nvarchar(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProductCategories_SeoKey",
            table: "ProductCategories",
            column: "SeoKey",
            unique: true,
            filter: "[SeoKey] IS NOT NULL");

        migrationBuilder.Sql("""
            UPDATE TOP (1) category
            SET SeoKey = N'solar-panels'
            FROM ProductCategories category
            WHERE category.IsActive = 1
              AND EXISTS (
                  SELECT 1
                  FROM ProductCategoryLanguages language
                  WHERE language.ProductCategoryId = category.Id
                    AND language.LanguageCode = 1
                    AND language.CategoryName IN (
                        N'Gunes panel', N'Gunes paneli', N'Günəş paneli',
                        N'Günəş panelləri', N'Solar panel'));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ProductCategories_SeoKey",
            table: "ProductCategories");

        migrationBuilder.DropColumn(
            name: "SeoKey",
            table: "ProductCategories");
    }
}
