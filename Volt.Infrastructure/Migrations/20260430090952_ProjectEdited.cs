using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectEdited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Position",
                table: "Projects",
                newName: "TotalPower");

            migrationBuilder.AddColumn<int>(
                name: "AnnualProduction",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte>(
                name: "AnnualProductionType",
                table: "Projects",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "PowerType",
                table: "Projects",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "SystemType",
                table: "Projects",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "Location1",
                table: "ProjectLanguages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location2",
                table: "ProjectLanguages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnnualProduction",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AnnualProductionType",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PowerType",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SystemType",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Location1",
                table: "ProjectLanguages");

            migrationBuilder.DropColumn(
                name: "Location2",
                table: "ProjectLanguages");

            migrationBuilder.RenameColumn(
                name: "TotalPower",
                table: "Projects",
                newName: "Position");
        }
    }
}
