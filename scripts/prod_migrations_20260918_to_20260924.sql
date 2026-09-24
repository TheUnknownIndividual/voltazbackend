-- All pending migrations (AI content jobs, news scraper, marketplace listings) in one idempotent script.
-- Safe to run more than once and on a database where some of it was already applied.
-- The API also runs pending migrations at startup, so this is a controlled pre-step, not a requirement.
-- Take a database backup first.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ContentAiGenerationJobs')
BEGIN
    CREATE TABLE [ContentAiGenerationJobs] (
        [Id] uniqueidentifier NOT NULL,
        [CreatedByAdminId] int NOT NULL,
        [ContentType] nvarchar(16) NOT NULL,
        [ContentId] int NULL,
        [Status] nvarchar(32) NOT NULL,
        [RequestJson] nvarchar(max) NOT NULL,
        [DraftJson] nvarchar(max) NULL,
        [ErrorCode] nvarchar(100) NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ContentAiGenerationJobs] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_ContentAiGenerationJobs_CreatedByAdminId_Status] ON [ContentAiGenerationJobs] ([CreatedByAdminId], [Status]);
    CREATE INDEX [IX_ContentAiGenerationJobs_ExpiresAt] ON [ContentAiGenerationJobs] ([ExpiresAt]);
    CREATE INDEX [IX_ContentAiGenerationJobs_ContentType] ON [ContentAiGenerationJobs] ([ContentType]);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ScrapedNewsItems')
BEGIN
    CREATE TABLE [ScrapedNewsItems] (
        [Id] int NOT NULL IDENTITY(1,1),
        [SourceSite] nvarchar(32) NOT NULL,
        [SourceUrl] nvarchar(450) NOT NULL,
        [SourceTitle] nvarchar(500) NOT NULL,
        [SourcePublishedAt] datetime2 NOT NULL,
        [SourceListingImageUrl] nvarchar(1000) NULL,
        [SourceDetailImageUrlsJson] nvarchar(max) NULL,
        [RawBodyText] nvarchar(max) NULL,
        [RehostedImageUrl] nvarchar(1000) NULL,
        [Status] nvarchar(32) NOT NULL,
        [ContentAiGenerationJobId] uniqueidentifier NULL,
        [RelevanceReason] nvarchar(1000) NULL,
        [PublishedContentType] nvarchar(16) NULL,
        [PublishedContentId] int NULL,
        [PublishedAt] datetime2 NULL,
        [ErrorCode] nvarchar(100) NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        [DiscoveredAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ScrapedNewsItems] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_ScrapedNewsItems_SourceUrl] ON [ScrapedNewsItems] ([SourceUrl]);
    CREATE INDEX [IX_ScrapedNewsItems_SourceSite_SourcePublishedAt] ON [ScrapedNewsItems] ([SourceSite], [SourcePublishedAt]);
    CREATE INDEX [IX_ScrapedNewsItems_Status] ON [ScrapedNewsItems] ([Status]);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RenewableNewsScraperRunState')
BEGIN
    CREATE TABLE [RenewableNewsScraperRunState] (
        [Id] int NOT NULL,
        [LastRunDateUtc] date NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RenewableNewsScraperRunState] PRIMARY KEY ([Id])
    );
END

IF COL_LENGTH('ContentAiGenerationJobs', 'Origin') IS NULL
BEGIN
    ALTER TABLE [ContentAiGenerationJobs] ALTER COLUMN [CreatedByAdminId] int NULL;
    ALTER TABLE [ContentAiGenerationJobs] ADD [Origin] nvarchar(16) NOT NULL DEFAULT 'admin';
END

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

IF COL_LENGTH('MarketplaceListings', 'VariantId') IS NULL
    ALTER TABLE [MarketplaceListings] ADD [VariantId] int NULL;

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260918120000_AddContentAiGenerationJobs')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260918120000_AddContentAiGenerationJobs', N'8.0.24');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260918130000_AddScrapedNewsItems')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260918130000_AddScrapedNewsItems', N'8.0.24');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260918130100_AddContentAiGenerationJobOrigin')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260918130100_AddContentAiGenerationJobOrigin', N'8.0.24');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260924120000_AddMarketplaceListings')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260924120000_AddMarketplaceListings', N'8.0.24');
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260924150000_AddMarketplaceListingVariantId')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260924150000_AddMarketplaceListingVariantId', N'8.0.24');
