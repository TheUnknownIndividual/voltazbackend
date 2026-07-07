namespace Volt.Application.Interfaces
{
    public interface ISeoFeedService
    {
        Task<string> GenerateSitemapXmlAsync(CancellationToken ct = default);
        string GenerateRobotsTxt();
    }
}
