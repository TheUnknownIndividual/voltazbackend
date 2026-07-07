using Volt.Application.Dtos;
using Volt.Application.Dtos.Product;
using Volt.Application.Dtos.Search;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class SearchService : ISearchService
    {
        private readonly IUnitOfWork _uow;
        private readonly IProductSearchService _productSearchService;

        public SearchService(IUnitOfWork uow, IProductSearchService productSearchService)
        {
            _uow = uow;
            _productSearchService = productSearchService;
        }

        public async Task<ApiResponse<SearchResultDto>> SearchAsync(
            string query,
            int productLimit,
            LanguageCode? languageCode = null,
            CancellationToken ct = default)
        {
            var normalizedQuery = ProductSearchHelper.Normalize(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return ApiResponse<SearchResultDto>.SuccessResponse(new SearchResultDto(
                    new SearchProductGroupDto(Array.Empty<ProductDto>(), 0),
                    Array.Empty<SearchCategoryResultDto>(),
                    Array.Empty<SearchServiceResultDto>(),
                    Array.Empty<SearchUsefulPageResultDto>()));
            }

            var safeProductLimit = productLimit > 0 ? Math.Min(productLimit, 20) : 6;
            var categories = await _uow.Repository<ProductCategory>().ListNoTrackingAsync(x => x.IsActive, ct);
            var categoryLanguages = await _uow.Repository<ProductCategoryLanguage>().ListNoTrackingAsync(x => x.IsActive, ct);
            var subCategories = await _uow.Repository<ProductSubCategory>().ListNoTrackingAsync(x => x.IsActive, ct);
            var subCategoryLanguages = await _uow.Repository<ProductSubCategoryLanguage>().ListNoTrackingAsync(x => x.IsActive, ct);

            var matchingProducts = await _productSearchService.SearchAsync(normalizedQuery, ct: ct);
            var productDtos = matchingProducts
                .Take(safeProductLimit)
                .Select(x => MapToProductDto(
                    x.Product,
                    x.Images,
                    x.Parametrs,
                    x.Descriptions,
                    x.DescriptionLanguages,
                    x.PromotionIds))
                .ToList();

            var categoryResults = BuildCategoryResults(
                normalizedQuery,
                languageCode,
                categories,
                categoryLanguages,
                subCategories,
                subCategoryLanguages);
            var serviceResults = await BuildServiceResultsAsync(normalizedQuery, languageCode, ct);
            var usefulPages = await BuildUsefulPageResultsAsync(normalizedQuery, languageCode, ct);

            return ApiResponse<SearchResultDto>.SuccessResponse(new SearchResultDto(
                new SearchProductGroupDto(productDtos, matchingProducts.Count),
                categoryResults,
                serviceResults,
                usefulPages));
        }

        private static IReadOnlyList<SearchCategoryResultDto> BuildCategoryResults(
            string query,
            LanguageCode? languageCode,
            IEnumerable<ProductCategory> categories,
            IEnumerable<ProductCategoryLanguage> categoryLanguages,
            IEnumerable<ProductSubCategory> subCategories,
            IEnumerable<ProductSubCategoryLanguage> subCategoryLanguages)
        {
            var categoryResults = categories
                .Where(category => categoryLanguages.Any(language =>
                    language.ProductCategoryId == category.Id
                    && language.IsActive
                    && ProductSearchHelper.ContainsQuery(language.CategoryName, query)))
                .OrderBy(x => x.Id)
                .Select(category => new SearchCategoryResultDto(
                    "category",
                    category.Id,
                    null,
                    PickCategoryName(category.Id, categoryLanguages, languageCode)))
                .ToList();

            var activeCategoryIds = categories.Select(x => x.Id).ToHashSet();
            var subCategoryResults = subCategories
                .Where(subCategory => activeCategoryIds.Contains(subCategory.ProductCategoryId)
                    && subCategoryLanguages.Any(language =>
                        language.ProductSubCategoryId == subCategory.Id
                        && language.IsActive
                        && ProductSearchHelper.ContainsQuery(language.SubCategoryName, query)))
                .OrderBy(x => x.Id)
                .Select(subCategory => new SearchCategoryResultDto(
                    "subCategory",
                    subCategory.ProductCategoryId,
                    subCategory.Id,
                    PickSubCategoryName(subCategory.Id, subCategoryLanguages, languageCode)))
                .ToList();

            return categoryResults.Concat(subCategoryResults).ToList();
        }

        private async Task<IReadOnlyList<SearchServiceResultDto>> BuildServiceResultsAsync(
            string query,
            LanguageCode? languageCode,
            CancellationToken ct)
        {
            var services = await _uow.Repository<ServiceManagement>().ListNoTrackingAsync(x => x.IsActive, ct);
            var serviceIds = services.Select(x => x.Id).ToHashSet();
            var languages = await _uow.Repository<ServiceManagementLanguage>().ListNoTrackingAsync(
                x => serviceIds.Contains(x.ServiceMagamentId) && x.IsActive, ct);

            return services
                .Where(service => languages.Any(language =>
                    language.ServiceMagamentId == service.Id
                    && (ProductSearchHelper.ContainsQuery(language.Title, query)
                        || ProductSearchHelper.ContainsQuery(language.Description, query)
                        || ProductSearchHelper.ContainsQuery(language.Content1, query)
                        || ProductSearchHelper.ContainsQuery(language.Content2, query)
                        || ProductSearchHelper.ContainsQuery(language.Content3, query)
                        || ProductSearchHelper.ContainsQuery(language.Content4, query))))
                .OrderBy(x => x.Id)
                .Select(service =>
                {
                    var language = PickLanguage(languages.Where(x => x.ServiceMagamentId == service.Id), languageCode);
                    return new SearchServiceResultDto(service.Id, language?.Title ?? string.Empty, language?.Description ?? string.Empty);
                })
                .ToList();
        }

        private async Task<IReadOnlyList<SearchUsefulPageResultDto>> BuildUsefulPageResultsAsync(
            string query,
            LanguageCode? languageCode,
            CancellationToken ct)
        {
            var blogs = await _uow.Repository<Blog>().ListNoTrackingAsync(x => x.IsActive, ct);
            var blogIds = blogs.Select(x => x.Id).ToHashSet();
            var blogTranslations = await _uow.Repository<BlogTranslation>().ListNoTrackingAsync(
                x => blogIds.Contains(x.BlogId) && x.IsActive, ct);

            var blogResults = blogs
                .Where(blog => blogTranslations.Any(translation =>
                    translation.BlogId == blog.Id
                    && (ProductSearchHelper.ContainsQuery(translation.Title, query)
                        || ProductSearchHelper.ContainsQuery(translation.Description, query)
                        || ProductSearchHelper.ContainsQuery(translation.Content, query))))
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .Select(blog =>
                {
                    var translation = PickLanguage(blogTranslations.Where(x => x.BlogId == blog.Id), languageCode);
                    return new SearchUsefulPageResultDto(
                        "blog",
                        blog.Id,
                        translation?.Title ?? string.Empty,
                        translation?.Description ?? string.Empty,
                        "blog",
                        "/blog");
                });

            var newsPosts = await _uow.Repository<NewsPost>().ListNoTrackingAsync(x => x.IsActive, ct);
            var newsIds = newsPosts.Select(x => x.Id).ToHashSet();
            var newsLanguages = await _uow.Repository<NewsPostLanguage>().ListNoTrackingAsync(
                x => newsIds.Contains(x.NewsPostId) && x.IsActive, ct);

            var newsResults = newsPosts
                .Where(news => newsLanguages.Any(language =>
                    language.NewsPostId == news.Id
                    && (ProductSearchHelper.ContainsQuery(language.Title, query)
                        || ProductSearchHelper.ContainsQuery(language.Description, query)
                        || ProductSearchHelper.ContainsQuery(language.Content, query))))
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .Select(news =>
                {
                    var language = PickLanguage(newsLanguages.Where(x => x.NewsPostId == news.Id), languageCode);
                    return new SearchUsefulPageResultDto(
                        "news",
                        news.Id,
                        language?.Title ?? string.Empty,
                        language?.Description ?? string.Empty,
                        "news",
                        "/news");
                });

            return blogResults.Concat(newsResults).ToList();
        }

        private async Task<Dictionary<int, IReadOnlyList<int>>> GetActivePromotionIdsByProductAsync(
            HashSet<int> productIds,
            CancellationToken ct)
        {
            if (productIds.Count == 0)
            {
                return new Dictionary<int, IReadOnlyList<int>>();
            }

            var links = await _uow.Repository<ProductPromotion>().ListNoTrackingAsync(
                x => productIds.Contains(x.ProductId), ct);
            if (links.Count == 0)
            {
                return new Dictionary<int, IReadOnlyList<int>>();
            }

            var promotionIds = links.Select(x => x.PromotionId).ToHashSet();
            var activePromotionIds = (await _uow.Repository<Promotion>().ListNoTrackingAsync(
                x => promotionIds.Contains(x.Id) && x.IsActive, ct))
                .Select(x => x.Id)
                .ToHashSet();

            return links
                .Where(x => activePromotionIds.Contains(x.PromotionId))
                .GroupBy(x => x.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<int>)g.Select(x => x.PromotionId).OrderBy(id => id).ToList());
        }

        private static ProductDto MapToProductDto(
            Product product,
            IEnumerable<ProductImage> images,
            IEnumerable<ProductParametr> parametrs,
            IEnumerable<ProductDescription> descriptions,
            IEnumerable<ProductDescriptionLanguage> descriptionLanguages,
            IReadOnlyList<int> promotionIds)
        {
            var descriptionList = descriptions
                .Select(d => new ProductDescriptionDto(
                    d.Id,
                    descriptionLanguages
                        .Where(l => l.ProductDescriptionId == d.Id)
                        .Select(l => new ProductDescriptionLanguageDto(l.LanguageCode, l.Description, l.Features))
                        .ToList()))
                .ToList();

            return new ProductDto(
                product.Id,
                product.ProductName,
                product.ProductCategoryId,
                product.ProductSubCategoryId,
                product.ProductBrandId,
                product.ProductTechnologyId,
                product.InStock,
                product.OutOfStockAt,
                product.InHomePage,
                product.Certificate,
                images.Where(x => x.Type).Select(x => x.ImageUrl).ToList(),
                images.Where(x => !x.Type).Select(x => x.ImageUrl).ToList(),
                parametrs.Select(x => new ProductParametrDto(x.TechnicalPower, x.Effectiveness, x.Count, x.Amount)).ToList(),
                descriptionList,
                promotionIds);
        }

        private static string PickCategoryName(
            int categoryId,
            IEnumerable<ProductCategoryLanguage> languages,
            LanguageCode? languageCode)
            => PickLanguage(languages.Where(x => x.ProductCategoryId == categoryId && x.IsActive), languageCode)?.CategoryName ?? string.Empty;

        private static string PickSubCategoryName(
            int subCategoryId,
            IEnumerable<ProductSubCategoryLanguage> languages,
            LanguageCode? languageCode)
            => PickLanguage(languages.Where(x => x.ProductSubCategoryId == subCategoryId && x.IsActive), languageCode)?.SubCategoryName ?? string.Empty;

        private static TLanguage PickLanguage<TLanguage>(
            IEnumerable<TLanguage> languages,
            LanguageCode? languageCode)
            where TLanguage : class
        {
            var list = languages.ToList();
            if (languageCode.HasValue)
            {
                var matched = list.FirstOrDefault(x => x switch
                {
                    ProductCategoryLanguage category => category.LanguageCode == languageCode.Value,
                    ProductSubCategoryLanguage subCategory => subCategory.LanguageCode == languageCode.Value,
                    ServiceManagementLanguage service => service.LanguageCode == languageCode.Value,
                    BlogTranslation blog => blog.LanguageCode == languageCode.Value,
                    NewsPostLanguage news => news.LanguageCode == languageCode.Value,
                    _ => false
                });

                if (matched is not null)
                {
                    return matched;
                }
            }

            return list.OrderBy(x => x switch
            {
                ProductCategoryLanguage category => category.Id,
                ProductSubCategoryLanguage subCategory => subCategory.Id,
                ServiceManagementLanguage service => service.Id,
                BlogTranslation blog => blog.Id,
                NewsPostLanguage news => news.Id,
                _ => 0
            }).FirstOrDefault();
        }
    }
}
