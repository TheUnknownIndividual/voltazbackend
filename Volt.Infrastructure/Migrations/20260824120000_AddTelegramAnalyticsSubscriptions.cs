using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260824120000_AddTelegramAnalyticsSubscriptions")]
public sealed class AddTelegramAnalyticsSubscriptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ReceivesQiymetlendirmeNotifications",
            table: "AdminUsers",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "ReceivesYoxlaNotifications",
            table: "AdminUsers",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ReceivesQiymetlendirmeNotifications", table: "AdminUsers");
        migrationBuilder.DropColumn(name: "ReceivesYoxlaNotifications", table: "AdminUsers");
    }
}
