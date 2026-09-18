using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260918120000_AddContentAiGenerationJobs")]
    public sealed class AddContentAiGenerationJobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentAiGenerationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByAdminId = table.Column<int>(type: "int", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ContentAiGenerationJobs", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_ContentAiGenerationJobs_CreatedByAdminId_Status",
                table: "ContentAiGenerationJobs",
                columns: new[] { "CreatedByAdminId", "Status" });
            migrationBuilder.CreateIndex(
                name: "IX_ContentAiGenerationJobs_ExpiresAt",
                table: "ContentAiGenerationJobs",
                column: "ExpiresAt");
            migrationBuilder.CreateIndex(
                name: "IX_ContentAiGenerationJobs_ContentType",
                table: "ContentAiGenerationJobs",
                column: "ContentType");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropTable(name: "ContentAiGenerationJobs");
    }
}
