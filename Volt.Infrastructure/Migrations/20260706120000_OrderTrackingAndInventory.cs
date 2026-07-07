using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260706120000_OrderTrackingAndInventory")]
    public partial class OrderTrackingAndInventory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptedTerms",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "Intent",
                table: "Orders",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<byte>(
                name: "Source",
                table: "Orders",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InventoryReleased",
                table: "OrderItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProductParametrId",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReservedQuantity",
                table: "OrderItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductParametrId",
                table: "OrderItems",
                column: "ProductParametrId");

            migrationBuilder.Sql(@"
UPDATE p
SET InStock = CASE WHEN EXISTS (
    SELECT 1
    FROM ProductParametrs pp
    WHERE pp.ProductId = p.Id
      AND pp.IsActive = 1
      AND ISNULL(pp.Count, 0) > 0
) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
FROM Products p
WHERE p.IsActive = 1;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderItems_ProductParametrId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "AcceptedTerms",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Intent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InventoryReleased",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ProductParametrId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "OrderItems");
        }
    }
}
