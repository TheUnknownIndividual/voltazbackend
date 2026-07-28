using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

/// <summary>Adds HR-owned employment and salary payment dates without changing existing staff assignments.</summary>
[DbContext(typeof(DataContext))]
[Migration("20260723110000_AddHumanResourcesEmploymentDates")]
public sealed class AddHumanResourcesEmploymentDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "EmploymentStartDate",
            table: "AdminUsers",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "SalaryPaymentDate",
            table: "AdminUsers",
            type: "date",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmploymentStartDate", table: "AdminUsers");
        migrationBuilder.DropColumn(name: "SalaryPaymentDate", table: "AdminUsers");
    }
}
