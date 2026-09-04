using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260901072000_AddMetaInboxHistorySync")]
    public sealed class AddMetaInboxHistorySync : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHistorical",
                table: "MetaInboxMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MetaInboxHistorySyncs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneNumberId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MetaRequestId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Phase = table.Column<int>(type: "int", nullable: true),
                    LastChunkOrder = table.Column<int>(type: "int", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaInboxHistorySyncs", x => x.Id);
                    table.CheckConstraint("CK_MetaInboxHistorySyncs_Progress", "[Progress] >= 0 AND [Progress] <= 100");
                    table.CheckConstraint("CK_MetaInboxHistorySyncs_Phase", "[Phase] IS NULL OR ([Phase] >= 0 AND [Phase] <= 2)");
                    table.CheckConstraint("CK_MetaInboxHistorySyncs_LastChunkOrder", "[LastChunkOrder] IS NULL OR [LastChunkOrder] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "MetaInboxWhatsAppContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhoneNumberId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParticipantExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaInboxWhatsAppContacts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetaInboxHistorySyncs_PhoneNumberId",
                table: "MetaInboxHistorySyncs",
                column: "PhoneNumberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetaInboxWhatsAppContacts_PhoneNumberId_ParticipantExternalId",
                table: "MetaInboxWhatsAppContacts",
                columns: new[] { "PhoneNumberId", "ParticipantExternalId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MetaInboxWhatsAppContacts");
            migrationBuilder.DropTable(name: "MetaInboxHistorySyncs");
            migrationBuilder.DropColumn(name: "IsHistorical", table: "MetaInboxMessages");
        }
    }
}
