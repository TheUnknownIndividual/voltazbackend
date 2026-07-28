using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260721130000_AddAdminTelegramConnectionTokens")]
public sealed class AddAdminTelegramConnectionTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AdminTelegramConnectionTokens",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                AdminUserId = table.Column<int>(type: "int", nullable: false),
                TokenHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                RedeemedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                RedeemedChatId = table.Column<long>(type: "bigint", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdminTelegramConnectionTokens", x => x.Id);
                table.ForeignKey("FK_AdminTelegramConnectionTokens_AdminUsers_AdminUserId", x => x.AdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_AdminTelegramConnectionTokens_TokenHash", table: "AdminTelegramConnectionTokens", column: "TokenHash", unique: true);
        migrationBuilder.CreateIndex(name: "IX_AdminTelegramConnectionTokens_AdminUserId_ExpiresAt", table: "AdminTelegramConnectionTokens", columns: new[] { "AdminUserId", "ExpiresAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "AdminTelegramConnectionTokens");
}
