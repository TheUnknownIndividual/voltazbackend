using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260803180000_AddHomePageCategoryPresentation")]
public partial class AddHomePageCategoryPresentation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "HomePageDisplayOrder",
            table: "ProductCategories",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "HomePageProductId",
            table: "ProductCategories",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "ShowOnHomePage",
            table: "ProductCategories",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_ProductCategories_HomePageProductId",
            table: "ProductCategories",
            column: "HomePageProductId");

        migrationBuilder.AddForeignKey(
            name: "FK_ProductCategories_Products_HomePageProductId",
            table: "ProductCategories",
            column: "HomePageProductId",
            principalTable: "Products",
            principalColumn: "Id",
            onDelete: ReferentialAction.NoAction);

        SeedCategory(migrationBuilder, "İnvertorlar", 1, 249);
        SeedCategory(migrationBuilder, "Günəş paneli", 2, 420);
        SeedCategory(migrationBuilder, "Kabel və naqillər", 3, 37);
        SeedCategory(migrationBuilder, "Elektrik sistemləri", 4, 52);
        SeedCategory(migrationBuilder, "Fotovoltaik sistemlərin mühafizəsi", 5, 176);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ProductCategories_Products_HomePageProductId",
            table: "ProductCategories");

        migrationBuilder.DropIndex(
            name: "IX_ProductCategories_HomePageProductId",
            table: "ProductCategories");

        migrationBuilder.DropColumn(name: "HomePageDisplayOrder", table: "ProductCategories");
        migrationBuilder.DropColumn(name: "HomePageProductId", table: "ProductCategories");
        migrationBuilder.DropColumn(name: "ShowOnHomePage", table: "ProductCategories");
    }

    private static void SeedCategory(
        MigrationBuilder migrationBuilder,
        string categoryName,
        int displayOrder,
        int preferredProductId)
    {
        var escapedName = categoryName.Replace("'", "''");
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
                    AND language.CategoryName = '{{escapedName}}');
            """);
    }
}
