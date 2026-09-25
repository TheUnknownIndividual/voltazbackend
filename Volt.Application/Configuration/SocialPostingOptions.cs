namespace Volt.Application.Configuration
{
    // Automatic Facebook + Instagram posting. Tokens/IDs must be supplied through the
    // git-ignored social-posting.production.json (or environment variables), never appsettings.
    public sealed class SocialPostingOptions
    {
        public bool Enabled { get; set; }
        // When true the pipeline runs and stores captions but publishes nothing.
        public bool DryRun { get; set; } = true;

        public int MaxPostsPerDay { get; set; } = 1;
        public int MaxProductPostsPerWeek { get; set; } = 2;
        // Target share of news among posts (the rest are product promotions).
        public double NewsShare { get; set; } = 0.7;
        public int PostWindowStartHourLocal { get; set; } = 10;
        public int PostWindowEndHourLocal { get; set; } = 20;

        public int ProductCooldownDays { get; set; } = 45;
        public int NewsMaxAgeHours { get; set; } = 48;
        public int MinNewsBodyChars { get; set; } = 400;

        public int MinQualityScore { get; set; } = 80;
        public int RecentPostsForDedupe { get; set; } = 30;
        public double MaxCaptionSimilarity { get; set; } = 0.5;
        public int MaxHashtags { get; set; } = 5;
        public int MaxEmojis { get; set; } = 2;
        public int MaxCaptionChars { get; set; } = 900;
        public string[] BrandHashtags { get; set; } = new[] { "VoltAz", "GünəşEnerjisi", "BərpaOlunanEnerji", "Solar" };

        public int MaxImageBytes { get; set; } = 8 * 1024 * 1024;
        public int MaxAttempts { get; set; } = 3;

        public string GraphApiVersion { get; set; } = "v26.0";
        public string FacebookPageId { get; set; } = string.Empty;
        public string FacebookPageAccessToken { get; set; } = string.Empty;
        public string InstagramUserId { get; set; } = string.Empty;
        public string InstagramAccessToken { get; set; } = string.Empty;
        // Instagram tokens live 60 days and are not auto-refreshed; used only to warn the admin.
        public DateTime? InstagramTokenIssuedOn { get; set; }
    }
}
