using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260918130100_AddContentAiGenerationJobOrigin")]
    public sealed class AddContentAiGenerationJobOrigin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CreatedByAdminId",
                table: "ContentAiGenerationJobs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "ContentAiGenerationJobs",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "admin");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Origin", table: "ContentAiGenerationJobs");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByAdminId",
                table: "ContentAiGenerationJobs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
