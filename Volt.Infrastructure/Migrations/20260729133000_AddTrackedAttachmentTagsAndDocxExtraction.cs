using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations;

[DbContext(typeof(DataContext))]
[Migration("20260729133000_AddTrackedAttachmentTagsAndDocxExtraction")]
public partial class AddTrackedAttachmentTagsAndDocxExtraction : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Tag", table: "AdminTrackedProjectAttachments", type: "nvarchar(40)", maxLength: 40, nullable: false, defaultValue: "Qiymət təklifi");
        migrationBuilder.AddColumn<DateTime>(name: "CreatedAt", table: "AdminTrackedProjectAttachments", type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()");
        migrationBuilder.AddColumn<string>(name: "DocumentText", table: "AdminTrackedProjectAttachments", type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "DocumentExtractedAt", table: "AdminTrackedProjectAttachments", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>(name: "DocumentExtractionStatus", table: "AdminTrackedProjectAttachments", type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "NotRequired");
        migrationBuilder.AddColumn<string>(name: "DocumentExtractionError", table: "AdminTrackedProjectAttachments", type: "nvarchar(300)", maxLength: 300, nullable: true);

        // Existing records did not retain their upload timestamp. The project
        // update/creation timestamp is the closest truthful legacy indicator.
        migrationBuilder.Sql(@"
UPDATE attachment
SET attachment.CreatedAt = COALESCE(project.UpdatedAt, project.CreatedAt, attachment.CreatedAt),
    attachment.Tag = N'Qiymət təklifi',
    attachment.DocumentExtractionStatus = CASE
        WHEN LOWER(attachment.FileName) LIKE '%.docx' OR LOWER(attachment.FilePath) LIKE '%.docx%' THEN N'Pending'
        ELSE N'NotRequired'
    END
FROM AdminTrackedProjectAttachments attachment
INNER JOIN AdminTrackedProjects project ON project.Id = attachment.AdminTrackedProjectId;");

        migrationBuilder.CreateIndex(
            name: "IX_AdminTrackedProjectAttachments_DocumentExtractionStatus_CreatedAt",
            table: "AdminTrackedProjectAttachments",
            columns: new[] { "DocumentExtractionStatus", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_AdminTrackedProjectAttachments_DocumentExtractionStatus_CreatedAt", table: "AdminTrackedProjectAttachments");
        migrationBuilder.DropColumn(name: "Tag", table: "AdminTrackedProjectAttachments");
        migrationBuilder.DropColumn(name: "CreatedAt", table: "AdminTrackedProjectAttachments");
        migrationBuilder.DropColumn(name: "DocumentText", table: "AdminTrackedProjectAttachments");
        migrationBuilder.DropColumn(name: "DocumentExtractedAt", table: "AdminTrackedProjectAttachments");
        migrationBuilder.DropColumn(name: "DocumentExtractionStatus", table: "AdminTrackedProjectAttachments");
        migrationBuilder.DropColumn(name: "DocumentExtractionError", table: "AdminTrackedProjectAttachments");
    }
}
