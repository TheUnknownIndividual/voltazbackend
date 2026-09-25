-- Automatic social posting (Facebook + Instagram). Idempotent; the API also applies this migration at startup.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SocialPosts')
BEGIN
    CREATE TABLE [SocialPosts] (
        [Id] int NOT NULL IDENTITY(1,1),
        [SourceType] nvarchar(16) NOT NULL,
        [SourceId] int NOT NULL,
        [Platform] nvarchar(16) NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [Caption] nvarchar(2500) NULL,
        [ImageUrl] nvarchar(1000) NULL,
        [LinkUrl] nvarchar(500) NULL,
        [TopicKey] nvarchar(120) NULL,
        [QualityScore] int NULL,
        [RejectReason] nvarchar(500) NULL,
        [ExternalId] nvarchar(64) NULL,
        [PermalinkUrl] nvarchar(500) NULL,
        [Attempts] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [PostedAt] datetime2 NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SocialPosts] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_SocialPosts_Platform_SourceType_SourceId] ON [SocialPosts] ([Platform], [SourceType], [SourceId]);
    CREATE INDEX [IX_SocialPosts_CreatedAt] ON [SocialPosts] ([CreatedAt]);
    CREATE INDEX [IX_SocialPosts_Status] ON [SocialPosts] ([Status]);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SocialPostingState')
BEGIN
    CREATE TABLE [SocialPostingState] (
        [Id] int NOT NULL,
        [Paused] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SocialPostingState] PRIMARY KEY ([Id])
    );
END

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20260925120000_AddSocialPosts')
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES (N'20260925120000_AddSocialPosts', N'8.0.24');
