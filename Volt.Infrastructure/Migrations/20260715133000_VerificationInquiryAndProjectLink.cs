using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260715133000_VerificationInquiryAndProjectLink")]
    public partial class VerificationInquiryAndProjectLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "AdminTrackedProjectId", table: "DocumentLogs", type: "int", nullable: true);
            migrationBuilder.AddColumn<string>(name: "RevocationReason", table: "DocumentVerifications", type: "nvarchar(1000)", maxLength: 1000, nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentVerificationInquiries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    DocumentVerificationId = table.Column<int>(type: "int", nullable: false),
                    AdminTrackedProjectId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AssignedAdminUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVerificationInquiries", x => x.Id);
                    table.ForeignKey("FK_DocumentVerificationInquiries_AdminTrackedProjects_AdminTrackedProjectId", x => x.AdminTrackedProjectId, "AdminTrackedProjects", "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_DocumentVerificationInquiries_AdminUsers_AssignedAdminUserId", x => x.AssignedAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_DocumentVerificationInquiries_DocumentVerifications_DocumentVerificationId", x => x.DocumentVerificationId, "DocumentVerifications", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_DocumentLogs_AdminTrackedProjectId", table: "DocumentLogs", column: "AdminTrackedProjectId");
            migrationBuilder.CreateIndex(name: "IX_DocumentVerificationInquiries_AdminTrackedProjectId", table: "DocumentVerificationInquiries", column: "AdminTrackedProjectId");
            migrationBuilder.CreateIndex(name: "IX_DocumentVerificationInquiries_AssignedAdminUserId", table: "DocumentVerificationInquiries", column: "AssignedAdminUserId");
            migrationBuilder.CreateIndex(name: "IX_DocumentVerificationInquiries_DocumentVerificationId", table: "DocumentVerificationInquiries", column: "DocumentVerificationId");
            migrationBuilder.CreateIndex(name: "IX_DocumentVerificationInquiries_Status_CreatedAt", table: "DocumentVerificationInquiries", columns: new[] { "Status", "CreatedAt" });
            migrationBuilder.AddForeignKey(name: "FK_DocumentLogs_AdminTrackedProjects_AdminTrackedProjectId", table: "DocumentLogs", column: "AdminTrackedProjectId", principalTable: "AdminTrackedProjects", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_DocumentLogs_AdminTrackedProjects_AdminTrackedProjectId", table: "DocumentLogs");
            migrationBuilder.DropTable(name: "DocumentVerificationInquiries");
            migrationBuilder.DropIndex(name: "IX_DocumentLogs_AdminTrackedProjectId", table: "DocumentLogs");
            migrationBuilder.DropColumn(name: "AdminTrackedProjectId", table: "DocumentLogs");
            migrationBuilder.DropColumn(name: "RevocationReason", table: "DocumentVerifications");
        }
    }
}
