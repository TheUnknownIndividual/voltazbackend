namespace Volt.Application.Configuration
{
    public sealed class RenewableNewsScraperOptions
    {
        public bool Enabled { get; set; }
        public int RunHourLocal { get; set; } = 7;
        public int LookbackDays { get; set; } = 3;
        public int MaxPagesPerSource { get; set; } = 5;
        public int InterArticleDelaySeconds { get; set; } = 5;
        public string[] RelevanceKeywords { get; set; } = new[]
        {
            "günəş", "solar", "panel", "fotoelektrik", "PV", "bərpa olunan enerji",
            "yenilənən enerji", "yaşıl enerji", "invertor", "şəbəkəyə qoşulma",
        };
        public int MaxImageBytes { get; set; } = 8 * 1024 * 1024;
        public int ImageDownloadTimeoutSeconds { get; set; } = 20;
    }
}
