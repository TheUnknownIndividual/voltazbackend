using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260717090000_AdminProjectEditPermission")]
    public partial class AdminProjectEditPermission : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanEditProjects",
                table: "AdminUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanEditProjects",
                table: "AdminUsers");
        }
    }
}
