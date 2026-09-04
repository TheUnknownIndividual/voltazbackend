using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260901090000_AddMetaWebhookQueue")]
    public sealed class AddMetaWebhookQueue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetaWebhookQueueItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayloadHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaWebhookQueueItems", x => x.Id);
                    table.CheckConstraint("CK_MetaWebhookQueueItems_Attempts", "[Attempts] >= 0 AND [Attempts] <= 5");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetaWebhookQueueItems_CompletedAt",
                table: "MetaWebhookQueueItems",
                column: "CompletedAt");
            migrationBuilder.CreateIndex(
                name: "IX_MetaWebhookQueueItems_PayloadHash",
                table: "MetaWebhookQueueItems",
                column: "PayloadHash",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_MetaWebhookQueueItems_Status_NextAttemptAt",
                table: "MetaWebhookQueueItems",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.DropTable(name: "MetaWebhookQueueItems");
    }
}
