namespace Volt.Application.Configuration
{
    public sealed class RenewableNewsScraperOptions
    {
        public bool Enabled { get; set; }
        public int RunHourLocal { get; set; } = 7;
        public int LookbackDays { get; set; } = 3;
        public int MaxPagesPerSource { get; set; } = 5;
        // renewables.az categories to scan (its own dedicated solar and national sections by default).
        public string[] RenewablesAzCategories { get; set; } = new[] { "solar", "national" };
        public int InterArticleDelaySeconds { get; set; } = 5;
        public string[] RelevanceKeywords { get; set; } = new[]
        {
            "günəş", "solar", "panel", "fotoelektrik", "PV", "bərpa olunan enerji",
            "yenilənən enerji", "yaşıl enerji", "invertor", "şəbəkəyə qoşulma",
            "saxlama", "batareya", "GES", "külək", "elektrik stansiyası", "MVt",
        };
        public int MaxImageBytes { get; set; } = 8 * 1024 * 1024;
        public int ImageDownloadTimeoutSeconds { get; set; } = 20;

        // The VPS cannot reach minenergy.gov.az/area.gov.az directly (outbound blocked).
        // When set, all fetches to those sites are routed through an allowlisted relay
        // instead of being made directly. Leave both empty to fetch directly as before.
        public string? RelayBaseUrl { get; set; }
        public string? RelayAuthToken { get; set; }
    }
}
