using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

/// <summary>
/// Adds the stakeholder notification designation and lets an execution record reference
/// a tracked-project record. Existing public-project execution records remain untouched.
/// </summary>
[DbContext(typeof(DataContext))]
[Migration("20260721150000_AddStakeholderProjectApprovalWorkflow")]
public sealed class AddStakeholderProjectApprovalWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsStakeholder",
            table: "AdminUsers",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.DropForeignKey(
            name: "FK_ExecutionProjects_Projects_ProjectId",
            table: "ExecutionProjects");

        migrationBuilder.DropIndex(
            name: "IX_ExecutionProjects_ProjectId",
            table: "ExecutionProjects");

        migrationBuilder.AlterColumn<int>(
            name: "ProjectId",
            table: "ExecutionProjects",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");

        migrationBuilder.AddColumn<int>(
            name: "AdminTrackedProjectId",
            table: "ExecutionProjects",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExecutionProjects_ProjectId",
            table: "ExecutionProjects",
            column: "ProjectId",
            unique: true,
            filter: "[ProjectId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ExecutionProjects_AdminTrackedProjectId",
            table: "ExecutionProjects",
            column: "AdminTrackedProjectId",
            unique: true,
            filter: "[AdminTrackedProjectId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_ExecutionProjects_Projects_ProjectId",
            table: "ExecutionProjects",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_ExecutionProjects_AdminTrackedProjects_AdminTrackedProjectId",
            table: "ExecutionProjects",
            column: "AdminTrackedProjectId",
            principalTable: "AdminTrackedProjects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [ExecutionProjects] WHERE [AdminTrackedProjectId] IS NOT NULL)
                THROW 51000, 'Cannot roll back while accepted tracked projects exist in ExecutionProjects.', 1;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_ExecutionProjects_AdminTrackedProjects_AdminTrackedProjectId",
            table: "ExecutionProjects");

        migrationBuilder.DropForeignKey(
            name: "FK_ExecutionProjects_Projects_ProjectId",
            table: "ExecutionProjects");

        migrationBuilder.DropIndex(
            name: "IX_ExecutionProjects_AdminTrackedProjectId",
            table: "ExecutionProjects");

        migrationBuilder.DropIndex(
            name: "IX_ExecutionProjects_ProjectId",
            table: "ExecutionProjects");

        migrationBuilder.DropColumn(
            name: "AdminTrackedProjectId",
            table: "ExecutionProjects");

        migrationBuilder.AlterColumn<int>(
            name: "ProjectId",
            table: "ExecutionProjects",
            type: "int",
            nullable: false,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExecutionProjects_ProjectId",
            table: "ExecutionProjects",
            column: "ProjectId",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_ExecutionProjects_Projects_ProjectId",
            table: "ExecutionProjects",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.DropColumn(
            name: "IsStakeholder",
            table: "AdminUsers");
    }
}
