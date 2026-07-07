namespace Volt.Application.Configuration
{
    public sealed class SeoOptions
    {
        public string SiteBaseUrl { get; set; } = "https://volt.az";
        public string IndexNowKey { get; set; } = string.Empty;
        public string IndexNowEndpoint { get; set; } = "https://api.indexnow.org/indexnow";
        public bool EnableIndexNow { get; set; } = true;
        public bool EnableSearchConsoleSitemapSubmit { get; set; } = false;
        public string SearchConsoleSiteUrl { get; set; } = "https://volt.az/";
        public string SearchConsoleSitemapUrl { get; set; } = "https://volt.az/sitemap.xml";
        public string SearchConsoleAccessToken { get; set; } = string.Empty;
        public string SearchConsoleServiceAccountJsonPath { get; set; } = string.Empty;
        public int SearchConsoleSubmitThrottleMinutes { get; set; } = 60;
    }
}
