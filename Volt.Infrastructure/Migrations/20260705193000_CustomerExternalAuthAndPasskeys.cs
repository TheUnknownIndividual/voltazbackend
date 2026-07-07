using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Volt.Infrastructure.Data;

#nullable disable

namespace Volt.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DataContext))]
    [Migration("20260705193000_CustomerExternalAuthAndPasskeys")]
    public partial class CustomerExternalAuthAndPasskeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerUsers_Phone",
                table: "CustomerUsers");

            migrationBuilder.AlterColumn<byte[]>(
                name: "PasswordSalt",
                table: "CustomerUsers",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<byte[]>(
                name: "PasswordHash",
                table: "CustomerUsers",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "CustomerUsers",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateTable(
                name: "CustomerExternalLogins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ProviderSubject = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerExternalLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerExternalLogins_CustomerUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "CustomerUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomerPasskeyCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<int>(type: "int", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: false),
                    CredentialIdBase64Url = table.Column<string>(type: "nvarchar(1400)", maxLength: 1400, nullable: false),
                    PublicKey = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UserHandle = table.Column<byte[]>(type: "varbinary(128)", maxLength: 128, nullable: false),
                    SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                    CredType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AaGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerPasskeyCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerPasskeyCredentials_CustomerUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "CustomerUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUsers_Phone",
                table: "CustomerUsers",
                column: "Phone",
                unique: true,
                filter: "[Phone] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerExternalLogins_CustomerUserId",
                table: "CustomerExternalLogins",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerExternalLogins_Email",
                table: "CustomerExternalLogins",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerExternalLogins_Provider_ProviderSubject",
                table: "CustomerExternalLogins",
                columns: new[] { "Provider", "ProviderSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPasskeyCredentials_CredentialId",
                table: "CustomerPasskeyCredentials",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPasskeyCredentials_CredentialIdBase64Url",
                table: "CustomerPasskeyCredentials",
                column: "CredentialIdBase64Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPasskeyCredentials_CustomerUserId",
                table: "CustomerPasskeyCredentials",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerPasskeyCredentials_UserHandle",
                table: "CustomerPasskeyCredentials",
                column: "UserHandle");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerExternalLogins");

            migrationBuilder.DropTable(
                name: "CustomerPasskeyCredentials");

            migrationBuilder.DropIndex(
                name: "IX_CustomerUsers_Phone",
                table: "CustomerUsers");

            migrationBuilder.AlterColumn<byte[]>(
                name: "PasswordSalt",
                table: "CustomerUsers",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: Array.Empty<byte>(),
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte[]>(
                name: "PasswordHash",
                table: "CustomerUsers",
                type: "varbinary(max)",
                nullable: false,
                defaultValue: Array.Empty<byte>(),
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "CustomerUsers",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerUsers_Phone",
                table: "CustomerUsers",
                column: "Phone",
                unique: true);
        }
    }
}
