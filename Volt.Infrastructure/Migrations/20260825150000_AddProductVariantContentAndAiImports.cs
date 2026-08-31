using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260825150000_AddProductVariantContentAndAiImports")]
public sealed class AddProductVariantContentAndAiImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UseCommonVariantContent",
            table: "Products",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "ModelLabel",
            table: "ProductParametrs",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ProductAiImportJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedByAdminId = table.Column<int>(type: "int", nullable: false),
                ProductId = table.Column<int>(type: "int", nullable: true),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ProductAiImportJobs", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ProductParametrLanguages",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ProductParametrId = table.Column<int>(type: "int", nullable: false),
                LanguageCode = table.Column<int>(type: "int", nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: string.Empty),
                Features = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: string.Empty),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductParametrLanguages", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductParametrLanguages_ProductParametrs_ProductParametrId",
                    column: x => x.ProductParametrId,
                    principalTable: "ProductParametrs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProductAiImportJobs_CreatedByAdminId_Status",
            table: "ProductAiImportJobs",
            columns: new[] { "CreatedByAdminId", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_ProductAiImportJobs_ExpiresAt",
            table: "ProductAiImportJobs",
            column: "ExpiresAt");
        migrationBuilder.CreateIndex(
            name: "IX_ProductParametrLanguages_ProductParametrId_LanguageCode",
            table: "ProductParametrLanguages",
            columns: new[] { "ProductParametrId", "LanguageCode" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProductAiImportJobs");
        migrationBuilder.DropTable(name: "ProductParametrLanguages");
        migrationBuilder.DropColumn(name: "UseCommonVariantContent", table: "Products");
        migrationBuilder.DropColumn(name: "ModelLabel", table: "ProductParametrs");
    }
}
