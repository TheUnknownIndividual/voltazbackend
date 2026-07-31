using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260729120000_AddPublicAgentContactDrafts")]
public partial class AddPublicAgentContactDrafts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PublicAgentContactDrafts",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AccessTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Surname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                ApplicationTypeId = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                SubmittedContactRequestId = table.Column<int>(type: "int", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_PublicAgentContactDrafts", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_PublicAgentContactDrafts_PublicId",
            table: "PublicAgentContactDrafts",
            column: "PublicId",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_PublicAgentContactDrafts_Status_ExpiresAt",
            table: "PublicAgentContactDrafts",
            columns: new[] { "Status", "ExpiresAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PublicAgentContactDrafts");
    }
}
