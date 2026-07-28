using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260715120000_DocumentVerificationAndAdminAccess")]
    public partial class DocumentVerificationAndAdminAccess : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "DisplayName", table: "AdminUsers", type: "nvarchar(160)", maxLength: 160, nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<bool>(name: "IsSuperAdmin", table: "AdminUsers", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "CanDeleteProjects", table: "AdminUsers", type: "bit", nullable: false, defaultValue: false);
            migrationBuilder.Sql("UPDATE [AdminUsers] SET [DisplayName] = [Username], [IsSuperAdmin] = CAST(1 AS bit), [CanDeleteProjects] = CAST(1 AS bit) WHERE [Role] = 1");

            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<int>(type: "int", nullable: true),
                    ActorUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                    table.ForeignKey("FK_AdminAuditLogs_AdminUsers_AdminUserId", x => x.AdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "AdminPagePermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    AdminUserId = table.Column<int>(type: "int", nullable: false),
                    Page = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPagePermissions", x => x.Id);
                    table.ForeignKey("FK_AdminPagePermissions_AdminUsers_AdminUserId", x => x.AdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentVerifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    DocumentLogId = table.Column<int>(type: "int", nullable: false),
                    PublicToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DocumentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IssuerDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByAdminUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVerifications", x => x.Id);
                    table.ForeignKey("FK_DocumentVerifications_DocumentLogs_DocumentLogId", x => x.DocumentLogId, "DocumentLogs", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_DocumentVerifications_AdminUsers_RevokedByAdminUserId", x => x.RevokedByAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(name: "IX_AdminAuditLogs_AdminUserId_CreatedAt", table: "AdminAuditLogs", columns: new[] { "AdminUserId", "CreatedAt" });
            migrationBuilder.CreateIndex(name: "IX_AdminAuditLogs_CreatedAt", table: "AdminAuditLogs", column: "CreatedAt");
            migrationBuilder.CreateIndex(name: "IX_AdminPagePermissions_AdminUserId_Page", table: "AdminPagePermissions", columns: new[] { "AdminUserId", "Page" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_DocumentVerifications_DocumentLogId", table: "DocumentVerifications", column: "DocumentLogId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_DocumentVerifications_ExpiresAt", table: "DocumentVerifications", column: "ExpiresAt");
            migrationBuilder.CreateIndex(name: "IX_DocumentVerifications_PublicToken", table: "DocumentVerifications", column: "PublicToken", unique: true);
            migrationBuilder.CreateIndex(name: "IX_DocumentVerifications_RevokedByAdminUserId", table: "DocumentVerifications", column: "RevokedByAdminUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AdminAuditLogs");
            migrationBuilder.DropTable(name: "AdminPagePermissions");
            migrationBuilder.DropTable(name: "DocumentVerifications");
            migrationBuilder.DropColumn(name: "DisplayName", table: "AdminUsers");
            migrationBuilder.DropColumn(name: "IsSuperAdmin", table: "AdminUsers");
            migrationBuilder.DropColumn(name: "CanDeleteProjects", table: "AdminUsers");
        }
    }
}
