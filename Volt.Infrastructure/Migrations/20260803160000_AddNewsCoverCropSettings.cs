using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260803160000_AddNewsCoverCropSettings")]
public partial class AddNewsCoverCropSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CoverImagePositionX",
            table: "NewsPosts",
            type: "int",
            nullable: false,
            defaultValue: 50);

        migrationBuilder.AddColumn<int>(
            name: "CoverImagePositionY",
            table: "NewsPosts",
            type: "int",
            nullable: false,
            defaultValue: 50);

        migrationBuilder.AddColumn<decimal>(
            name: "CoverImageZoom",
            table: "NewsPosts",
            type: "decimal(4,2)",
            precision: 4,
            scale: 2,
            nullable: false,
            defaultValue: 1m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CoverImagePositionX", table: "NewsPosts");
        migrationBuilder.DropColumn(name: "CoverImagePositionY", table: "NewsPosts");
        migrationBuilder.DropColumn(name: "CoverImageZoom", table: "NewsPosts");
    }
}
