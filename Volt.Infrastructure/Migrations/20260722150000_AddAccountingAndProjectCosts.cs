using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

/// <summary>Additive accounting model. Existing project, user and warehouse rows are preserved.</summary>
[DbContext(typeof(DataContext))]
[Migration("20260722150000_AddAccountingAndProjectCosts")]
public sealed class AddAccountingAndProjectCosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "MonthlySalary", table: "AdminUsers", type: "decimal(18,2)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "StartDate", table: "ExecutionProjectStaff", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "EndDate", table: "ExecutionProjectStaff", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "UnitCost", table: "ExecutionWarehouseMovements", type: "decimal(18,2)", nullable: true);

        migrationBuilder.CreateTable(
            name: "ExecutionProjectExternalWorkers",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                ExecutionProjectId = table.Column<int>(type: "int", nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                CreatedByAdminUserId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExecutionProjectExternalWorkers", x => x.Id);
                table.ForeignKey("FK_ExecutionProjectExternalWorkers_ExecutionProjects_ExecutionProjectId", x => x.ExecutionProjectId, "ExecutionProjects", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_ExecutionProjectExternalWorkers_AdminUsers_CreatedByAdminUserId", x => x.CreatedByAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ExecutionProjectExpenses",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                ExecutionProjectId = table.Column<int>(type: "int", nullable: false),
                Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false, defaultValue: "Müxtəlif xərclər"),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReceiptUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                CreatedByAdminUserId = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExecutionProjectExpenses", x => x.Id);
                table.ForeignKey("FK_ExecutionProjectExpenses_ExecutionProjects_ExecutionProjectId", x => x.ExecutionProjectId, "ExecutionProjects", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_ExecutionProjectExpenses_AdminUsers_CreatedByAdminUserId", x => x.CreatedByAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectExternalWorkers_ExecutionProjectId_StartDate", table: "ExecutionProjectExternalWorkers", columns: new[] { "ExecutionProjectId", "StartDate" });
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectExternalWorkers_CreatedByAdminUserId", table: "ExecutionProjectExternalWorkers", column: "CreatedByAdminUserId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectExpenses_ExecutionProjectId_ExpenseDate", table: "ExecutionProjectExpenses", columns: new[] { "ExecutionProjectId", "ExpenseDate" });
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectExpenses_CreatedByAdminUserId", table: "ExecutionProjectExpenses", column: "CreatedByAdminUserId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectStaff_ExecutionProjectId_StartDate", table: "ExecutionProjectStaff", columns: new[] { "ExecutionProjectId", "StartDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ExecutionProjectExternalWorkers");
        migrationBuilder.DropTable(name: "ExecutionProjectExpenses");
        migrationBuilder.DropIndex(name: "IX_ExecutionProjectStaff_ExecutionProjectId_StartDate", table: "ExecutionProjectStaff");
        migrationBuilder.DropColumn(name: "MonthlySalary", table: "AdminUsers");
        migrationBuilder.DropColumn(name: "StartDate", table: "ExecutionProjectStaff");
        migrationBuilder.DropColumn(name: "EndDate", table: "ExecutionProjectStaff");
        migrationBuilder.DropColumn(name: "UnitCost", table: "ExecutionWarehouseMovements");
    }
}
