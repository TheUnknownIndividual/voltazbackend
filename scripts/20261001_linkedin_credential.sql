-- LinkedIn Organization OAuth credential (single row). Idempotent; the API also applies this at startup.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LinkedInCredential')
BEGIN
    CREATE TABLE [LinkedInCredential] (
        [Id] int NOT NULL,
        [OrganizationUrn] nvarchar(100) NULL,
        [AccessToken] nvarchar(4000) NULL,
        [RefreshToken] nvarchar(4000) NULL,
        [AccessTokenExpiresAtUtc] datetime2 NULL,
        [RefreshTokenExpiresAtUtc] datetime2 NULL,
        [ConnectedByAdminId] int NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_LinkedInCredential] PRIMARY KEY ([Id])
    );
END

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261001090000_AddLinkedInCredential')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20261001090000_AddLinkedInCredential', N'8.0.24');
