using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20260812120000_AddMetaInbox")]
    public partial class AddMetaInbox : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetaInboxConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    AccountExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParticipantExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParticipantDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParticipantAvatarUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AssignedAdminUserId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    UnreadCount = table.Column<int>(type: "int", nullable: false),
                    LastMessagePreview = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaInboxConversations", x => x.Id);
                    table.ForeignKey("FK_MetaInboxConversations_AdminUsers_AssignedAdminUserId", x => x.AssignedAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MetaInboxInternalNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<int>(type: "int", nullable: false),
                    AuthorAdminUserId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaInboxInternalNotes", x => x.Id);
                    table.ForeignKey("FK_MetaInboxInternalNotes_AdminUsers_AuthorAdminUserId", x => x.AuthorAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_MetaInboxInternalNotes_MetaInboxConversations_ConversationId", x => x.ConversationId, "MetaInboxConversations", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetaInboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<int>(type: "int", nullable: false),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AttachmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SentByAdminUserId = table.Column<int>(type: "int", nullable: true),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaInboxMessages", x => x.Id);
                    table.ForeignKey("FK_MetaInboxMessages_AdminUsers_SentByAdminUserId", x => x.SentByAdminUserId, "AdminUsers", "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_MetaInboxMessages_MetaInboxConversations_ConversationId", x => x.ConversationId, "MetaInboxConversations", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_MetaInboxConversations_AssignedAdminUserId", "MetaInboxConversations", "AssignedAdminUserId");
            migrationBuilder.CreateIndex("IX_MetaInboxConversations_Status_LastMessageAt", "MetaInboxConversations", new[] { "Status", "LastMessageAt" });
            migrationBuilder.CreateIndex("IX_MetaInboxConversations_Channel_AccountExternalId_ParticipantExternalId", "MetaInboxConversations", new[] { "Channel", "AccountExternalId", "ParticipantExternalId" }, unique: true);
            migrationBuilder.CreateIndex("IX_MetaInboxInternalNotes_AuthorAdminUserId", "MetaInboxInternalNotes", "AuthorAdminUserId");
            migrationBuilder.CreateIndex("IX_MetaInboxInternalNotes_ConversationId_Id", "MetaInboxInternalNotes", new[] { "ConversationId", "Id" });
            migrationBuilder.CreateIndex("IX_MetaInboxMessages_SentByAdminUserId", "MetaInboxMessages", "SentByAdminUserId");
            migrationBuilder.CreateIndex("IX_MetaInboxMessages_ConversationId_Id", "MetaInboxMessages", new[] { "ConversationId", "Id" });
            migrationBuilder.CreateIndex("IX_MetaInboxMessages_ConversationId_ExternalMessageId", "MetaInboxMessages", new[] { "ConversationId", "ExternalMessageId" }, unique: true, filter: "[ExternalMessageId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("MetaInboxInternalNotes");
            migrationBuilder.DropTable("MetaInboxMessages");
            migrationBuilder.DropTable("MetaInboxConversations");
        }
    }
}
