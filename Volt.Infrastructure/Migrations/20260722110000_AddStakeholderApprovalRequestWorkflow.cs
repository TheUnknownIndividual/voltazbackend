using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260722110000_AddStakeholderApprovalRequestWorkflow")]
public sealed class AddStakeholderApprovalRequestWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "ArchivedAt", table: "ExecutionProjects", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(name: "ArchiveReason", table: "ExecutionProjects", type: "nvarchar(250)", maxLength: 250, nullable: false, defaultValue: "");

        migrationBuilder.CreateTable(
            name: "StakeholderApprovalRequests",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                AdminTrackedProjectId = table.Column<int>(type: "int", nullable: false),
                EnvironmentScope = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                DispatchCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                ResolvedByAdminUserId = table.Column<int>(type: "int", nullable: true),
                CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CancellationReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StakeholderApprovalRequests", x => x.Id);
                table.ForeignKey("FK_StakeholderApprovalRequests_AdminTrackedProjects_AdminTrackedProjectId", x => x.AdminTrackedProjectId, "AdminTrackedProjects", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StakeholderApprovalRecipients",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                StakeholderApprovalRequestId = table.Column<int>(type: "int", nullable: false),
                AdminUserId = table.Column<int>(type: "int", nullable: false),
                TelegramChatId = table.Column<long>(type: "bigint", nullable: false),
                DeliveryStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                FailureStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StakeholderApprovalRecipients", x => x.Id);
                table.ForeignKey("FK_StakeholderApprovalRecipients_StakeholderApprovalRequests_StakeholderApprovalRequestId", x => x.StakeholderApprovalRequestId, "StakeholderApprovalRequests", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_StakeholderApprovalRequests_AdminTrackedProjectId_Status", table: "StakeholderApprovalRequests", columns: new[] { "AdminTrackedProjectId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_StakeholderApprovalRecipients_StakeholderApprovalRequestId_AdminUserId", table: "StakeholderApprovalRecipients", columns: new[] { "StakeholderApprovalRequestId", "AdminUserId" }, unique: true);

        // The old implementation had no per-recipient delivery evidence. Never retain a
        // false waiting state: non-accepted projects need no approval; accepted projects
        // must be made explicitly retryable.
        migrationBuilder.Sql(@"
UPDATE [AdminTrackedProjects]
SET [StakeholderApprovalStatus] = N'NotRequired',
    [StakeholderApprovalRequestedAt] = NULL,
    [StakeholderApprovalResolvedAt] = NULL,
    [StakeholderApprovalResolvedByAdminUserId] = NULL
WHERE LOWER(REPLACE(ISNULL([CurrentStatus], N''), N'ə', N'e')) <> N'qebul edildi';

UPDATE [AdminTrackedProjects]
SET [StakeholderApprovalStatus] = N'DeliveryFailed',
    [StakeholderApprovalResolvedAt] = NULL,
    [StakeholderApprovalResolvedByAdminUserId] = NULL
WHERE LOWER(REPLACE(ISNULL([CurrentStatus], N''), N'ə', N'e')) = N'qebul edildi'
  AND [StakeholderApprovalStatus] IN (N'Pending', N'NoConnectedStakeholder', N'Dispatching');");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StakeholderApprovalRecipients");
        migrationBuilder.DropTable(name: "StakeholderApprovalRequests");
        migrationBuilder.DropColumn(name: "ArchivedAt", table: "ExecutionProjects");
        migrationBuilder.DropColumn(name: "ArchiveReason", table: "ExecutionProjects");
    }
}
