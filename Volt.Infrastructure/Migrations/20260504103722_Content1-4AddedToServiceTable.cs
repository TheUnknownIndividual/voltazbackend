using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Content14AddedToServiceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActiveStatus",
                table: "ServiceManagements");

            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "ServiceManagements");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "ServiceManagements");

            migrationBuilder.AddColumn<string>(
                name: "Content1",
                table: "ServiceManagementLanguages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content2",
                table: "ServiceManagementLanguages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content3",
                table: "ServiceManagementLanguages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content4",
                table: "ServiceManagementLanguages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Content1",
                table: "ServiceManagementLanguages");

            migrationBuilder.DropColumn(
                name: "Content2",
                table: "ServiceManagementLanguages");

            migrationBuilder.DropColumn(
                name: "Content3",
                table: "ServiceManagementLanguages");

            migrationBuilder.DropColumn(
                name: "Content4",
                table: "ServiceManagementLanguages");

            migrationBuilder.AddColumn<bool>(
                name: "ActiveStatus",
                table: "ServiceManagements",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "ServiceManagements",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "ServiceManagements",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
