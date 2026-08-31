using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260803170000_AddContentSeoFields")]
public partial class AddContentSeoFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddSeoColumns(migrationBuilder, "BlogTranslations");
        AddSeoColumns(migrationBuilder, "NewsPostLanguages");
        AddSeoColumns(migrationBuilder, "ServiceManagementLanguages");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropSeoColumns(migrationBuilder, "BlogTranslations");
        DropSeoColumns(migrationBuilder, "NewsPostLanguages");
        DropSeoColumns(migrationBuilder, "ServiceManagementLanguages");
    }

    private static void AddSeoColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<string>(name: "SeoTitle", table: table, type: "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>(name: "SeoDescription", table: table, type: "nvarchar(500)", maxLength: 500, nullable: true);
        migrationBuilder.AddColumn<string>(name: "SeoKeywords", table: table, type: "nvarchar(500)", maxLength: 500, nullable: true);
    }

    private static void DropSeoColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropColumn(name: "SeoTitle", table: table);
        migrationBuilder.DropColumn(name: "SeoDescription", table: table);
        migrationBuilder.DropColumn(name: "SeoKeywords", table: table);
    }
}
