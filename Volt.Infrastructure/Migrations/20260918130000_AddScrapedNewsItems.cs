using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260918130000_AddScrapedNewsItems")]
    public sealed class AddScrapedNewsItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScrapedNewsItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSite = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    SourceTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourcePublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceListingImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourceDetailImageUrlsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RawBodyText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RehostedImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContentAiGenerationJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelevanceReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PublishedContentType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    PublishedContentId = table.Column<int>(type: "int", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DiscoveredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_ScrapedNewsItems", x => x.Id));

            migrationBuilder.CreateTable(
                name: "RenewableNewsScraperRunState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LastRunDateUtc = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_RenewableNewsScraperRunState", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedNewsItems_SourceUrl",
                table: "ScrapedNewsItems",
                column: "SourceUrl",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_ScrapedNewsItems_SourceSite_SourcePublishedAt",
                table: "ScrapedNewsItems",
                columns: new[] { "SourceSite", "SourcePublishedAt" });
            migrationBuilder.CreateIndex(
                name: "IX_ScrapedNewsItems_Status",
                table: "ScrapedNewsItems",
                column: "Status");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScrapedNewsItems");
            migrationBuilder.DropTable(name: "RenewableNewsScraperRunState");
        }
    }
}
