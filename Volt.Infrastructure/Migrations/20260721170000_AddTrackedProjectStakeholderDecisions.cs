using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260721170000_AddTrackedProjectStakeholderDecisions")]
public sealed class AddTrackedProjectStakeholderDecisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "StakeholderApprovalStatus", table: "AdminTrackedProjects", type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "NotRequired");
        migrationBuilder.AddColumn<DateTime>(name: "StakeholderApprovalRequestedAt", table: "AdminTrackedProjects", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "StakeholderApprovalResolvedAt", table: "AdminTrackedProjects", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<int>(name: "StakeholderApprovalResolvedByAdminUserId", table: "AdminTrackedProjects", type: "int", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_AdminTrackedProjects_StakeholderApprovalStatus", table: "AdminTrackedProjects", column: "StakeholderApprovalStatus");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AdminTrackedProjects_StakeholderApprovalStatus", table: "AdminTrackedProjects");
        migrationBuilder.DropColumn(name: "StakeholderApprovalStatus", table: "AdminTrackedProjects");
        migrationBuilder.DropColumn(name: "StakeholderApprovalRequestedAt", table: "AdminTrackedProjects");
        migrationBuilder.DropColumn(name: "StakeholderApprovalResolvedAt", table: "AdminTrackedProjects");
        migrationBuilder.DropColumn(name: "StakeholderApprovalResolvedByAdminUserId", table: "AdminTrackedProjects");
    }
}
