using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260716120000_SolarProjectTrackerLink")]
    public partial class SolarProjectTrackerLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdminTrackedProjectId",
                table: "SolarSalesProjects",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolarSalesProjects_AdminTrackedProjectId",
                table: "SolarSalesProjects",
                column: "AdminTrackedProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_SolarSalesProjects_AdminTrackedProjects_AdminTrackedProjectId",
                table: "SolarSalesProjects",
                column: "AdminTrackedProjectId",
                principalTable: "AdminTrackedProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SolarSalesProjects_AdminTrackedProjects_AdminTrackedProjectId",
                table: "SolarSalesProjects");

            migrationBuilder.DropIndex(
                name: "IX_SolarSalesProjects_AdminTrackedProjectId",
                table: "SolarSalesProjects");

            migrationBuilder.DropColumn(
                name: "AdminTrackedProjectId",
                table: "SolarSalesProjects");
        }
    }
}
