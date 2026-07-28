using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

/// <summary>
/// Additive migration only. It does not alter existing Projects or warehouse stock data,
/// making rollback a simple drop of the new delivery-workspace tables.
/// </summary>
[DbContext(typeof(DataContext))]
[Migration("20260721113000_AddExecutionProjects")]
public sealed class AddExecutionProjects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "CanApproveWarehouseMovements", table: "AdminUsers", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<long>(name: "TelegramChatId", table: "AdminUsers", type: "bigint", nullable: true);
        migrationBuilder.CreateTable(
            name: "ExecutionProjects",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                ProjectId = table.Column<int>(type: "int", nullable: false),
                ProjectManagerAdminUserId = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "Aktiv"),
                PlannedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                PlannedEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExecutionProjects", x => x.Id);
                table.ForeignKey("FK_ExecutionProjects_AdminUsers_ProjectManagerAdminUserId", x => x.ProjectManagerAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ExecutionProjects_Projects_ProjectId", x => x.ProjectId, "Projects", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ExecutionProjectBoqItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                ExecutionProjectId = table.Column<int>(type: "int", nullable: false), ProductId = table.Column<int>(type: "int", nullable: true),
                ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false), Unit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                PlannedQuantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false), UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false), CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => { table.PrimaryKey("PK_ExecutionProjectBoqItems", x => x.Id); table.ForeignKey("FK_ExecutionProjectBoqItems_ExecutionProjects_ExecutionProjectId", x => x.ExecutionProjectId, "ExecutionProjects", "Id", onDelete: ReferentialAction.Cascade); table.ForeignKey("FK_ExecutionProjectBoqItems_Products_ProductId", x => x.ProductId, "Products", "Id", onDelete: ReferentialAction.SetNull); });

        migrationBuilder.CreateTable(
            name: "ExecutionProjectStaff",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), ExecutionProjectId = table.Column<int>(type: "int", nullable: false), AdminUserId = table.Column<int>(type: "int", nullable: false),
                RoleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false), AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            }, constraints: table => { table.PrimaryKey("PK_ExecutionProjectStaff", x => x.Id); table.ForeignKey("FK_ExecutionProjectStaff_ExecutionProjects_ExecutionProjectId", x => x.ExecutionProjectId, "ExecutionProjects", "Id", onDelete: ReferentialAction.Cascade); table.ForeignKey("FK_ExecutionProjectStaff_AdminUsers_AdminUserId", x => x.AdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict); });

        migrationBuilder.CreateTable(
            name: "ExecutionProjectTasks",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), ExecutionProjectId = table.Column<int>(type: "int", nullable: false), AssignedAdminUserId = table.Column<int>(type: "int", nullable: false), CreatedByAdminUserId = table.Column<int>(type: "int", nullable: false),
                Title = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false), Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false), DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false, defaultValue: "Assigned"), NotificationStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "PendingRecipientLink"), CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false), CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
            }, constraints: table => { table.PrimaryKey("PK_ExecutionProjectTasks", x => x.Id); table.ForeignKey("FK_ExecutionProjectTasks_ExecutionProjects_ExecutionProjectId", x => x.ExecutionProjectId, "ExecutionProjects", "Id", onDelete: ReferentialAction.Cascade); table.ForeignKey("FK_ExecutionProjectTasks_AdminUsers_AssignedAdminUserId", x => x.AssignedAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict); table.ForeignKey("FK_ExecutionProjectTasks_AdminUsers_CreatedByAdminUserId", x => x.CreatedByAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict); });

        migrationBuilder.CreateTable(
            name: "ExecutionWarehouseMovements",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"), ExecutionProjectId = table.Column<int>(type: "int", nullable: false), ProductId = table.Column<int>(type: "int", nullable: true), ProductParametrId = table.Column<int>(type: "int", nullable: true),
                ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false), Unit = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false), Quantity = table.Column<decimal>(type: "decimal(18,3)", nullable: false), Direction = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false), MovedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                RecordedByAdminUserId = table.Column<int>(type: "int", nullable: false), ApprovalStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "Pending"), ApprovedByAdminUserId = table.Column<int>(type: "int", nullable: true), ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true), ApprovalNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
            }, constraints: table => { table.PrimaryKey("PK_ExecutionWarehouseMovements", x => x.Id); table.ForeignKey("FK_ExecutionWarehouseMovements_ExecutionProjects_ExecutionProjectId", x => x.ExecutionProjectId, "ExecutionProjects", "Id", onDelete: ReferentialAction.Cascade); table.ForeignKey("FK_ExecutionWarehouseMovements_Products_ProductId", x => x.ProductId, "Products", "Id", onDelete: ReferentialAction.SetNull); table.ForeignKey("FK_ExecutionWarehouseMovements_ProductParametrs_ProductParametrId", x => x.ProductParametrId, "ProductParametrs", "Id", onDelete: ReferentialAction.Restrict); table.ForeignKey("FK_ExecutionWarehouseMovements_AdminUsers_RecordedByAdminUserId", x => x.RecordedByAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict); table.ForeignKey("FK_ExecutionWarehouseMovements_AdminUsers_ApprovedByAdminUserId", x => x.ApprovedByAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict); });

        migrationBuilder.CreateIndex(name: "IX_ExecutionProjects_ProjectId", table: "ExecutionProjects", column: "ProjectId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_AdminUsers_TelegramChatId", table: "AdminUsers", column: "TelegramChatId", unique: true, filter: "[TelegramChatId] IS NOT NULL");
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjects_ProjectManagerAdminUserId", table: "ExecutionProjects", column: "ProjectManagerAdminUserId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectBoqItems_ExecutionProjectId", table: "ExecutionProjectBoqItems", column: "ExecutionProjectId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectBoqItems_ProductId", table: "ExecutionProjectBoqItems", column: "ProductId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectStaff_AdminUserId", table: "ExecutionProjectStaff", column: "AdminUserId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectStaff_ExecutionProjectId_AdminUserId", table: "ExecutionProjectStaff", columns: new[] { "ExecutionProjectId", "AdminUserId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectTasks_AssignedAdminUserId_Status", table: "ExecutionProjectTasks", columns: new[] { "AssignedAdminUserId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectTasks_CreatedByAdminUserId", table: "ExecutionProjectTasks", column: "CreatedByAdminUserId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionProjectTasks_ExecutionProjectId", table: "ExecutionProjectTasks", column: "ExecutionProjectId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionWarehouseMovements_ApprovedByAdminUserId", table: "ExecutionWarehouseMovements", column: "ApprovedByAdminUserId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionWarehouseMovements_ProductId", table: "ExecutionWarehouseMovements", column: "ProductId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionWarehouseMovements_ProductParametrId", table: "ExecutionWarehouseMovements", column: "ProductParametrId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionWarehouseMovements_RecordedByAdminUserId", table: "ExecutionWarehouseMovements", column: "RecordedByAdminUserId");
        migrationBuilder.CreateIndex(name: "IX_ExecutionWarehouseMovements_ExecutionProjectId_MovedAt", table: "ExecutionWarehouseMovements", columns: new[] { "ExecutionProjectId", "MovedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ExecutionProjectBoqItems");
        migrationBuilder.DropTable(name: "ExecutionProjectStaff");
        migrationBuilder.DropTable(name: "ExecutionProjectTasks");
        migrationBuilder.DropTable(name: "ExecutionWarehouseMovements");
        migrationBuilder.DropTable(name: "ExecutionProjects");
        migrationBuilder.DropIndex(name: "IX_AdminUsers_TelegramChatId", table: "AdminUsers");
        migrationBuilder.DropColumn(name: "CanApproveWarehouseMovements", table: "AdminUsers");
        migrationBuilder.DropColumn(name: "TelegramChatId", table: "AdminUsers");
    }
}
