using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SolarAnalyticsCreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    CurrentNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolarSalesProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolarSalesProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolarCalculationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SolarSalesProjectId = table.Column<int>(type: "int", nullable: true),
                    AdminUserId = table.Column<int>(type: "int", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolarCalculationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SolarCalculationLogs_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SolarCalculationLogs_SolarSalesProjects_SolarSalesProjectId",
                        column: x => x.SolarSalesProjectId,
                        principalTable: "SolarSalesProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SolarSalesProjectId = table.Column<int>(type: "int", nullable: false),
                    SolarCalculationLogId = table.Column<int>(type: "int", nullable: true),
                    AdminUserId = table.Column<int>(type: "int", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLogs_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentLogs_SolarCalculationLogs_SolarCalculationLogId",
                        column: x => x.SolarCalculationLogId,
                        principalTable: "SolarCalculationLogs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentLogs_SolarSalesProjects_SolarSalesProjectId",
                        column: x => x.SolarSalesProjectId,
                        principalTable: "SolarSalesProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLogs_AdminUserId",
                table: "DocumentLogs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLogs_CreatedAt",
                table: "DocumentLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLogs_DocumentNumber",
                table: "DocumentLogs",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLogs_SolarCalculationLogId",
                table: "DocumentLogs",
                column: "SolarCalculationLogId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLogs_SolarSalesProjectId",
                table: "DocumentLogs",
                column: "SolarSalesProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentSequences_DocumentCode_Year_Month",
                table: "DocumentSequences",
                columns: new[] { "DocumentCode", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SolarCalculationLogs_AdminUserId",
                table: "SolarCalculationLogs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SolarCalculationLogs_CreatedAt",
                table: "SolarCalculationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SolarCalculationLogs_SolarSalesProjectId",
                table: "SolarCalculationLogs",
                column: "SolarSalesProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SolarCalculationLogs_Source_EventType_CreatedAt",
                table: "SolarCalculationLogs",
                columns: new[] { "Source", "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SolarSalesProjects_NormalizedName",
                table: "SolarSalesProjects",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentLogs");

            migrationBuilder.DropTable(
                name: "DocumentSequences");

            migrationBuilder.DropTable(
                name: "SolarCalculationLogs");

            migrationBuilder.DropTable(
                name: "SolarSalesProjects");
        }
    }
}
