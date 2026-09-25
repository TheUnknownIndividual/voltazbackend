using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260925120000_AddSocialPosts")]
    public sealed class AddSocialPosts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(2500)", maxLength: 2500, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LinkUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TopicKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    QualityScore = table.Column<int>(type: "int", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PermalinkUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_SocialPosts", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_Platform_SourceType_SourceId",
                table: "SocialPosts",
                columns: new[] { "Platform", "SourceType", "SourceId" },
                unique: true);
            migrationBuilder.CreateIndex(name: "IX_SocialPosts_CreatedAt", table: "SocialPosts", column: "CreatedAt");
            migrationBuilder.CreateIndex(name: "IX_SocialPosts_Status", table: "SocialPosts", column: "Status");

            migrationBuilder.CreateTable(
                name: "SocialPostingState",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Paused = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_SocialPostingState", x => x.Id));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SocialPostingState");
            migrationBuilder.DropTable(name: "SocialPosts");
        }
    }
}
