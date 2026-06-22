using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveServiceManagementIdFromApplicationType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApplicationTypes_ServiceManagements_ServiceManagementId",
                table: "ApplicationTypes");

            migrationBuilder.DropIndex(
                name: "IX_ApplicationTypes_ServiceManagementId",
                table: "ApplicationTypes");

            migrationBuilder.DropColumn(
                name: "ServiceManagementId",
                table: "ApplicationTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServiceManagementId",
                table: "ApplicationTypes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationTypes_ServiceManagementId",
                table: "ApplicationTypes",
                column: "ServiceManagementId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApplicationTypes_ServiceManagements_ServiceManagementId",
                table: "ApplicationTypes",
                column: "ServiceManagementId",
                principalTable: "ServiceManagements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
