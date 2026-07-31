using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class SeoFeedService : ISeoFeedService
    {
        private static readonly string[] StaticRoutes =
        [
            "/",
            "/about",
            "/services",
            "/solar-installation",
            "/projects",
            "/products",
            "/calculator",
            "/contact",
            "/videos",
            "/faq",
            "/how-to-start",
            "/necessary-documents",
            "/legislation",
            "/credits",
            "/partnership",
            "/pro-club",
            "/privacy-policy",
            "/terms-of-service",
            "/purchase-terms",
            "/news",
            "/blog"
        ];

        private readonly IUnitOfWork _uow;
        private readonly SeoOptions _options;

        public SeoFeedService(IUnitOfWork uow, IOptions<SeoOptions> options)
        {
            _uow = uow;
            _options = options.Value;
        }

        public async Task<string> GenerateSitemapXmlAsync(CancellationToken ct = default)
        {
            XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XNamespace imageNs = "http://www.google.com/schemas/sitemap-image/1.1";
            XNamespace xhtmlNs = "http://www.w3.org/1999/xhtml";

            var products = await _uow.Repository<Product>().ListNoTrackingAsync(x => x.IsActive, ct);
            var productIds = products.Select(x => x.Id).ToHashSet();
            var images = productIds.Count == 0
                ? new List<ProductImage>()
                : await _uow.Repository<ProductImage>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId) && x.Type, ct);
            var productDescriptions = productIds.Count == 0
                ? new List<ProductDescription>()
                : await _uow.Repository<ProductDescription>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId), ct);
            var productDescriptionIds = productDescriptions.Select(x => x.Id).ToHashSet();
            var productDescriptionLanguages = productDescriptionIds.Count == 0
                ? new List<ProductDescriptionLanguage>()
                : await _uow.Repository<ProductDescriptionLanguage>().ListNoTrackingAsync(x => productDescriptionIds.Contains(x.ProductDescriptionId) && x.IsActive, ct);

            var root = new XElement(ns + "urlset",
                new XAttribute(XNamespace.Xmlns + "image", imageNs),
                new XAttribute(XNamespace.Xmlns + "xhtml", xhtmlNs));

            foreach (var route in StaticRoutes)
            {
                root.Add(new XElement(ns + "url",
                    new XElement(ns + "loc", BuildAbsoluteUrl(route)),
                    new XElement(ns + "changefreq", route == "/calculator" ? "daily" : "weekly"),
                    new XElement(ns + "priority", GetStaticRoutePriority(route))));
            }

            foreach (var product in products.OrderByDescending(x => x.Id))
            {
                var url = new XElement(ns + "url",
                    new XElement(ns + "loc", BuildAbsoluteUrl($"/product/{product.Id}")),
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", "0.7"));

                foreach (var imageUrl in images
                    .Where(x => x.ProductId == product.Id)
                    .Select(x => NormalizeUrl(x.ImageUrl))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Take(3))
                {
                    url.Add(new XElement(imageNs + "image",
                        new XElement(imageNs + "loc", imageUrl),
                        new XElement(imageNs + "title", product.ProductName)));
                }

                root.Add(url);
            }

            var projects = await _uow.Repository<Project>().ListNoTrackingAsync(x => x.IsActive, ct);
            var projectIds = projects.Select(x => x.Id).ToHashSet();
            var projectImages = projectIds.Count == 0
                ? new List<ProjectImage>()
                : await _uow.Repository<ProjectImage>().ListNoTrackingAsync(x => projectIds.Contains(x.ProjectId) && x.IsActive, ct);
            var projectLanguages = projectIds.Count == 0
                ? new List<ProjectLanguage>()
                : await _uow.Repository<ProjectLanguage>().ListNoTrackingAsync(x => projectIds.Contains(x.ProjectId) && x.IsActive, ct);

            foreach (var project in projects.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt))
            {
                var title = projectLanguages
                    .Where(x => x.ProjectId == project.Id)
                    .OrderBy(x => x.Id)
                    .Select(x => x.Title)
                    .FirstOrDefault();
                var url = new XElement(ns + "url",
                    new XElement(ns + "loc", BuildAbsoluteUrl($"/projects/{project.Id}")),
                    new XElement(ns + "lastmod", (project.UpdatedAt ?? project.CreatedAt).ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "monthly"),
                    new XElement(ns + "priority", "0.75"));

                foreach (var imageUrl in projectImages
                    .Where(x => x.ProjectId == project.Id)
                    .Select(x => NormalizeUrl(x.ImagePath))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Take(3))
                {
                    url.Add(new XElement(imageNs + "image",
                        new XElement(imageNs + "loc", imageUrl),
                        new XElement(imageNs + "title", title ?? "Volt.az project")));
                }

                root.Add(url);
            }

            var blogs = await _uow.Repository<Blog>().ListNoTrackingAsync(x => x.IsActive, ct);
            var blogIds = blogs.Select(x => x.Id).ToHashSet();
            var blogTranslations = blogIds.Count == 0
                ? new List<BlogTranslation>()
                : await _uow.Repository<BlogTranslation>().ListNoTrackingAsync(x => blogIds.Contains(x.BlogId) && x.IsActive, ct);

            foreach (var blog in blogs.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt))
            {
                var title = blogTranslations
                    .Where(x => x.BlogId == blog.Id)
                    .OrderBy(x => x.Id)
                    .Select(x => x.Title)
                    .FirstOrDefault();
                var url = new XElement(ns + "url",
                    new XElement(ns + "loc", BuildAbsoluteUrl($"/blog/{blog.Id}")),
                    new XElement(ns + "lastmod", (blog.UpdatedAt ?? blog.CreatedAt).ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", "0.75"));
                var imageUrl = NormalizeUrl(blog.CoverImagePath);

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    url.Add(new XElement(imageNs + "image",
                        new XElement(imageNs + "loc", imageUrl),
                        new XElement(imageNs + "title", title ?? "Volt.az blog")));
                }

                root.Add(url);
            }

            var newsPosts = await _uow.Repository<NewsPost>().ListNoTrackingAsync(x => x.IsActive, ct);
            var newsIds = newsPosts.Select(x => x.Id).ToHashSet();
            var newsLanguages = newsIds.Count == 0
                ? new List<NewsPostLanguage>()
                : await _uow.Repository<NewsPostLanguage>().ListNoTrackingAsync(x => newsIds.Contains(x.NewsPostId) && x.IsActive, ct);

            foreach (var newsPost in newsPosts.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt))
            {
                var title = newsLanguages
                    .Where(x => x.NewsPostId == newsPost.Id)
                    .OrderBy(x => x.Id)
                    .Select(x => x.Title)
                    .FirstOrDefault();
                var url = new XElement(ns + "url",
                    new XElement(ns + "loc", BuildAbsoluteUrl($"/news/{newsPost.Id}")),
                    new XElement(ns + "lastmod", (newsPost.UpdatedAt ?? newsPost.CreatedAt).ToString("yyyy-MM-dd")),
                    new XElement(ns + "changefreq", "weekly"),
                    new XElement(ns + "priority", "0.75"));
                var imageUrl = NormalizeUrl(newsPost.CoverImagePath);

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    url.Add(new XElement(imageNs + "image",
                        new XElement(imageNs + "loc", imageUrl),
                        new XElement(imageNs + "title", title ?? "Volt.az news")));
                }

                root.Add(url);
            }

            var defaultLanguageUrls = root.Elements(ns + "url").ToList();
            root.RemoveNodes();
            var allLanguages = new[] { "az", "en", "ru", "tr" };
            var productLanguageMap = (from description in productDescriptions
                                      join language in productDescriptionLanguages on description.Id equals language.ProductDescriptionId
                                      group (int)language.LanguageCode by description.ProductId into grouped
                                      select grouped).ToDictionary(grouped => grouped.Key, grouped => grouped.AsEnumerable());
            var projectLanguageMap = projectLanguages.GroupBy(x => x.ProjectId).ToDictionary(grouped => grouped.Key, grouped => grouped.Select(x => (int)x.LanguageCode));
            var blogLanguageMap = blogTranslations.GroupBy(x => x.BlogId).ToDictionary(grouped => grouped.Key, grouped => grouped.Select(x => (int)x.LanguageCode));
            var newsLanguageMap = newsLanguages.GroupBy(x => x.NewsPostId).ToDictionary(grouped => grouped.Key, grouped => grouped.Select(x => (int)x.LanguageCode));

            IReadOnlyCollection<string> AvailableLanguages(string basePath)
            {
                var parts = basePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !int.TryParse(parts[1], out var entityId))
                {
                    return allLanguages;
                }

                IEnumerable<int> languageCodes = parts[0].ToLowerInvariant() switch
                {
                    "product" => productLanguageMap.GetValueOrDefault(entityId) ?? Enumerable.Empty<int>(),
                    "projects" => projectLanguageMap.GetValueOrDefault(entityId) ?? Enumerable.Empty<int>(),
                    "blog" => blogLanguageMap.GetValueOrDefault(entityId) ?? Enumerable.Empty<int>(),
                    "news" => newsLanguageMap.GetValueOrDefault(entityId) ?? Enumerable.Empty<int>(),
                    _ => Enumerable.Empty<int>()
                };

                return languageCodes
                    .Select(code => code switch { 2 => "en", 3 => "ru", 4 => "tr", _ => "az" })
                    .Append("az")
                    .Distinct()
                    .ToArray();
            }

            foreach (var sourceUrl in defaultLanguageUrls)
            {
                var absoluteLocation = sourceUrl.Element(ns + "loc")?.Value;
                if (!Uri.TryCreate(absoluteLocation, UriKind.Absolute, out var uri))
                {
                    continue;
                }

                var basePath = uri.AbsolutePath;
                var languages = AvailableLanguages(basePath);
                foreach (var language in languages)
                {
                    var localizedUrl = new XElement(sourceUrl);
                    localizedUrl.Element(ns + "loc")!.Value = BuildAbsoluteUrl(LocalizePath(basePath, language));
                    var alternateElements = languages
                        .Select(alternateLanguage => new XElement(xhtmlNs + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", alternateLanguage),
                            new XAttribute("href", BuildAbsoluteUrl(LocalizePath(basePath, alternateLanguage)))))
                        .Append(new XElement(xhtmlNs + "link",
                            new XAttribute("rel", "alternate"),
                            new XAttribute("hreflang", "x-default"),
                            new XAttribute("href", BuildAbsoluteUrl(LocalizePath(basePath, "az")))));

                    localizedUrl.Element(ns + "loc")!.AddAfterSelf(alternateElements);
                    root.Add(localizedUrl);
                }
            }

            return ToXmlString(new XDocument(new XDeclaration("1.0", "UTF-8", null), root));
        }

        public string GenerateRobotsTxt()
        {
            var baseUrl = (_options.SiteBaseUrl ?? "https://volt.az").Trim().TrimEnd('/');
            return string.Join(Environment.NewLine, new[]
            {
                "User-agent: *",
                "Allow: /",
                "",
                "Disallow: /admin",
                "Disallow: /admin-dashboard",
                "Disallow: /customer-dashboard",
                "Disallow: /pro-club/dashboard",
                "Disallow: /cart",
                "Disallow: /checkout",
                "Disallow: /order",
                "Disallow: /theme-lab",
                "",
                $"Sitemap: {baseUrl}/sitemap.xml",
                ""
            });
        }

        private static string GetStaticRoutePriority(string route)
            => route switch
            {
                "/" => "1.0",
                "/calculator" => "1.0",
                "/blog" or "/news" => "0.9",
                _ => "0.8"
            };

        private string BuildAbsoluteUrl(string path)
        {
            var baseUrl = (_options.SiteBaseUrl ?? "https://volt.az").Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(path) || path == "/")
            {
                return $"{baseUrl}/";
            }

            return $"{baseUrl}/{path.TrimStart('/')}";
        }

        private static string LocalizePath(string basePath, string language)
        {
            if (basePath.Equals("/solar-installation", StringComparison.OrdinalIgnoreCase))
            {
                return language switch
                {
                    "en" => "/en/solar-panel-installation",
                    "ru" => "/ru/ustanovka-solnechnyh-paneley",
                    "tr" => "/tr/gunes-paneli-kurulumu",
                    _ => "/gunes-paneli-qurasdirilmasi"
                };
            }

            if (language == "az")
            {
                return basePath;
            }

            return basePath == "/" ? $"/{language}" : $"/{language}{basePath}";
        }

        private string NormalizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return BuildAbsoluteUrl("/volt-logo.png");
            }

            var trimmed = url.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return BuildAbsoluteUrl(trimmed);
        }

        private static string ToXmlString(XDocument document)
            => document.Declaration + Environment.NewLine + document;
    }
}
