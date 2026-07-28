using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdminProjectTrackerCreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminTrackedProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    PersonName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ProjectDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InquiryReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OfferSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseExpectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CurrentStatus = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SmallNote = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: true),
                    OfferPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsOfferPriceManual = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminTrackedProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminTrackedProjectAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminTrackedProjectId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminTrackedProjectAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminTrackedProjectAttachments_AdminTrackedProjects_AdminTrackedProjectId",
                        column: x => x.AdminTrackedProjectId,
                        principalTable: "AdminTrackedProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminTrackedProjectOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdminTrackedProjectId = table.Column<int>(type: "int", nullable: false),
                    Power = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MountType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AreaType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ExtraAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminTrackedProjectOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminTrackedProjectOffers_AdminTrackedProjects_AdminTrackedProjectId",
                        column: x => x.AdminTrackedProjectId,
                        principalTable: "AdminTrackedProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminTrackedProjectAttachments_AdminTrackedProjectId",
                table: "AdminTrackedProjectAttachments",
                column: "AdminTrackedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminTrackedProjectOffers_AdminTrackedProjectId",
                table: "AdminTrackedProjectOffers",
                column: "AdminTrackedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminTrackedProjects_CreatedAt",
                table: "AdminTrackedProjects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminTrackedProjects_CurrentStatus",
                table: "AdminTrackedProjects",
                column: "CurrentStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminTrackedProjectAttachments");

            migrationBuilder.DropTable(
                name: "AdminTrackedProjectOffers");

            migrationBuilder.DropTable(
                name: "AdminTrackedProjects");
        }
    }
}
