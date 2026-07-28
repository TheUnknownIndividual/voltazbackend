using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260708120000_ProjectAdminDetails")]
    public partial class ProjectAdminDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactFullName",
                table: "Projects",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Projects",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStatus",
                table: "Projects",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InquiryReceivedAt",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OfferAmountAzn",
                table: "Projects",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OfferSentAt",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProjectDate",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResponseExpectedAt",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortNote",
                table: "Projects",
                type: "nvarchar(140)",
                maxLength: 140,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAttachments_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Power = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PowerType = table.Column<byte>(type: "tinyint", nullable: false),
                    AreaType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectOffers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAttachments_ProjectId",
                table: "ProjectAttachments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectOffers_ProjectId",
                table: "ProjectOffers",
                column: "ProjectId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectAttachments");

            migrationBuilder.DropTable(
                name: "ProjectOffers");

            migrationBuilder.DropColumn(
                name: "ContactFullName",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CurrentStatus",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "InquiryReceivedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OfferAmountAzn",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OfferSentAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ResponseExpectedAt",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ShortNote",
                table: "Projects");
        }
    }
}
