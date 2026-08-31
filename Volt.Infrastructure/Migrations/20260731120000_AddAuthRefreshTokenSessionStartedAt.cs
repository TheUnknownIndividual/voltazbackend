using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260731120000_AddAuthRefreshTokenSessionStartedAt")]
public partial class AddAuthRefreshTokenSessionStartedAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "SessionStartedAt",
            table: "AuthRefreshTokens",
            type: "datetime2",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE [AuthRefreshTokens]
            SET [SessionStartedAt] = [CreatedAt]
            WHERE [SessionStartedAt] IS NULL;
            """);

        migrationBuilder.AlterColumn<DateTime>(
            name: "SessionStartedAt",
            table: "AuthRefreshTokens",
            type: "datetime2",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SessionStartedAt",
            table: "AuthRefreshTokens");
    }
}
