using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectTableEdited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Location1",
                table: "ProjectLanguages");

            migrationBuilder.RenameColumn(
                name: "Location2",
                table: "ProjectLanguages",
                newName: "Location");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Location",
                table: "ProjectLanguages",
                newName: "Location2");

            migrationBuilder.AddColumn<string>(
                name: "Location1",
                table: "ProjectLanguages",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
