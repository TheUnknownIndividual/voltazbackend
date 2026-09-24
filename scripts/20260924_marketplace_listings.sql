-- Idempotent: safe to run more than once. Test applies this automatically via startup migrations;
-- run it by hand against production (Staging/Production databases are not migrated for you).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MarketplaceListings')
BEGIN
    CREATE TABLE [MarketplaceListings] (
        [Id] int NOT NULL IDENTITY(1,1),
        [ProductId] int NOT NULL,
        [Marketplace] nvarchar(16) NOT NULL,
        [ExternalId] nvarchar(64) NOT NULL,
        [Url] nvarchar(500) NULL,
        [Status] nvarchar(16) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MarketplaceListings] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_MarketplaceListings_ProductId] ON [MarketplaceListings] ([ProductId]);
    CREATE UNIQUE INDEX [IX_MarketplaceListings_Marketplace_ExternalId] ON [MarketplaceListings] ([Marketplace], [ExternalId]);
END

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260924120000_AddMarketplaceListings')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260924120000_AddMarketplaceListings', N'8.0.24');
