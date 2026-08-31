using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ProductSearchService : IProductSearchService
    {
        private const string CacheKey = "volt-product-search-catalog";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private readonly IUnitOfWork _uow;
        private readonly IMemoryCache _cache;

        public ProductSearchService(IUnitOfWork uow, IMemoryCache cache)
        {
            _uow = uow;
            _cache = cache;
        }

        public async Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
            string query,
            int? categoryId = null,
            int? subCategoryId = null,
            CancellationToken ct = default)
        {
            var searchQuery = ProductSearchHelper.CreateQuery(query);
            if (searchQuery.Terms.Count == 0)
            {
                return Array.Empty<ProductSearchResult>();
            }

            var catalog = await GetCatalogAsync(ct);
            var standalonePowerWatts = ParseStandalonePowerQueryWatts(query);
            return catalog.Documents
                .Where(x => (categoryId == null || x.Product.ProductCategoryId == categoryId)
                    && (subCategoryId == null || x.Product.ProductSubCategoryId == subCategoryId)
                    && (!standalonePowerWatts.HasValue
                        || HasEquivalentTechnicalPower(x.Parametrs, standalonePowerWatts.Value)))
                .Select(x => new
                {
                    Document = x,
                    Rank = ScoreDocument(x, searchQuery)
                })
                .Where(x => x.Rank.IsMatch)
                .OrderByDescending(x => x.Rank.Score)
                .ThenByDescending(x => x.Rank.ExactNameMatch)
                .ThenByDescending(x => x.Document.Product.InStock)
                .ThenByDescending(x => x.Document.Product.InHomePage)
                .ThenBy(x => x.Document.Product.Id)
                .Select(x => new ProductSearchResult(
                    x.Document.Product,
                    x.Document.Images,
                    x.Document.Parametrs,
                    x.Document.ProductDescriptions,
                    x.Document.DescriptionLanguages,
                    x.Document.PromotionIds,
                    x.Rank.Score,
                    x.Rank.ExactNameMatch))
                .ToList();
        }

        public void Invalidate()
            => _cache.Remove(CacheKey);

        private async Task<ProductSearchCatalog> GetCatalogAsync(CancellationToken ct)
        {
            if (_cache.TryGetValue(CacheKey, out ProductSearchCatalog cached))
            {
                return cached;
            }

            var catalog = await BuildCatalogAsync(ct);
            _cache.Set(CacheKey, catalog, CacheTtl);
            return catalog;
        }

        private async Task<ProductSearchCatalog> BuildCatalogAsync(CancellationToken ct)
        {
            var products = await _uow.Repository<Product>().ListNoTrackingAsync(x => x.IsActive, ct);
            if (products.Count == 0)
            {
                return new ProductSearchCatalog(Array.Empty<ProductSearchDocument>());
            }

            var categories = await _uow.Repository<ProductCategory>().ListNoTrackingAsync(x => x.IsActive, ct);
            var subCategories = await _uow.Repository<ProductSubCategory>().ListNoTrackingAsync(x => x.IsActive, ct);
            var activeCategoryIds = categories.Select(x => x.Id).ToHashSet();
            var activeSubCategoryIds = subCategories.Select(x => x.Id).ToHashSet();

            products = products
                .Where(x => activeCategoryIds.Contains(x.ProductCategoryId)
                    && activeSubCategoryIds.Contains(x.ProductSubCategoryId))
                .ToList();
            if (products.Count == 0)
            {
                return new ProductSearchCatalog(Array.Empty<ProductSearchDocument>());
            }

            var productIds = products.Select(x => x.Id).ToHashSet();
            var brands = await _uow.Repository<ProductBrand>().ListNoTrackingAsync(x => x.IsActive, ct);
            var technologies = await _uow.Repository<ProductTechnology>().ListNoTrackingAsync(x => x.IsActive, ct);
            var categoryLanguages = await _uow.Repository<ProductCategoryLanguage>().ListNoTrackingAsync(
                x => activeCategoryIds.Contains(x.ProductCategoryId) && x.IsActive, ct);
            var subCategoryLanguages = await _uow.Repository<ProductSubCategoryLanguage>().ListNoTrackingAsync(
                x => activeSubCategoryIds.Contains(x.ProductSubCategoryId) && x.IsActive, ct);
            var images = await _uow.Repository<ProductImage>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId), ct);
            var parametrs = await _uow.Repository<ProductParametr>().ListNoTrackingAsync(
                x => productIds.Contains(x.ProductId) && x.IsActive, ct);
            var descriptions = await _uow.Repository<ProductDescription>().ListNoTrackingAsync(
                x => productIds.Contains(x.ProductId), ct);
            var descriptionIds = descriptions.Select(x => x.Id).ToHashSet();
            var descriptionLanguages = descriptionIds.Count == 0
                ? new List<ProductDescriptionLanguage>()
                : await _uow.Repository<ProductDescriptionLanguage>().ListNoTrackingAsync(
                    x => descriptionIds.Contains(x.ProductDescriptionId) && x.IsActive, ct);

            var promotionIdsByProduct = await GetActivePromotionIdsByProductAsync(productIds, ct);
            var brandById = brands.ToDictionary(x => x.Id);
            var technologyById = technologies.ToDictionary(x => x.Id);
            var categoryNamesById = categoryLanguages
                .GroupBy(x => x.ProductCategoryId)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Select(l => l.CategoryName).ToList());
            var subCategoryNamesById = subCategoryLanguages
                .GroupBy(x => x.ProductSubCategoryId)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Select(l => l.SubCategoryName).ToList());
            var imagesByProduct = images.GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductImage>)x.ToList());
            var parametrsByProduct = parametrs.GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductParametr>)x.ToList());
            var descriptionsByProduct = descriptions.GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductDescription>)x.ToList());
            var descriptionProductById = descriptions.ToDictionary(x => x.Id, x => x.ProductId);
            var descriptionLanguagesByProduct = descriptionLanguages
                .Where(x => descriptionProductById.ContainsKey(x.ProductDescriptionId))
                .GroupBy(x => descriptionProductById[x.ProductDescriptionId])
                .ToDictionary(x => x.Key, x => (IReadOnlyList<ProductDescriptionLanguage>)x.ToList());

            var documents = products
                .Select(product =>
                {
                    var productParametrs = parametrsByProduct.GetValueOrDefault(product.Id, Array.Empty<ProductParametr>());
                    var productDescriptionLanguages = descriptionLanguagesByProduct.GetValueOrDefault(product.Id, Array.Empty<ProductDescriptionLanguage>());
                    return new ProductSearchDocument(
                        product,
                        imagesByProduct.GetValueOrDefault(product.Id, Array.Empty<ProductImage>()),
                        productParametrs,
                        descriptionsByProduct.GetValueOrDefault(product.Id, Array.Empty<ProductDescription>()),
                        productDescriptionLanguages,
                        promotionIdsByProduct.GetValueOrDefault(product.Id, Array.Empty<int>()),
                        brandById.GetValueOrDefault(product.ProductBrandId)?.Name ?? string.Empty,
                        product.ProductTechnologyId.HasValue
                            ? technologyById.GetValueOrDefault(product.ProductTechnologyId.Value)?.Name ?? string.Empty
                            : string.Empty,
                        categoryNamesById.GetValueOrDefault(product.ProductCategoryId, Array.Empty<string>()),
                        subCategoryNamesById.GetValueOrDefault(product.ProductSubCategoryId, Array.Empty<string>()),
                        BuildTechnicalPowerSearchValues(productParametrs),
                        productDescriptionLanguages.Select(x => x.Description ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
                        productDescriptionLanguages.Select(x => x.Features ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList());
                })
                .ToList();

            return new ProductSearchCatalog(documents);
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

        private static ProductRank ScoreDocument(ProductSearchDocument document, ProductSearchQuery query)
        {
            var score = 0d;
            var matchedTerms = new HashSet<int>();
            var strongMatchedTerms = new HashSet<int>();

            void AddScore(ProductSearchTextScore textScore, bool strong, double cap = double.MaxValue)
            {
                if (textScore.Score <= 0)
                {
                    return;
                }

                score += Math.Min(textScore.Score, cap);
                foreach (var index in textScore.MatchedTermIndexes)
                {
                    matchedTerms.Add(index);
                    if (strong)
                    {
                        strongMatchedTerms.Add(index);
                    }
                }
            }

            var nameScore = ProductSearchHelper.ScoreText(document.Product.ProductName, query, 120, 70, 40);
            AddScore(nameScore, true);
            var exactNameMatch = nameScore.PhraseMatch
                || ProductSearchHelper.NormalizeTokenPhrase(document.Product.ProductName) == query.NormalizedPhrase;

            AddScore(ProductSearchHelper.ScoreText(document.BrandName, query, 80, 50, 50), true);
            AddScore(ProductSearchHelper.ScoreText(document.TechnologyName, query, 55, 35, 38), true);
            AddScore(ScoreBest(document.CategoryNames, query, 50, 35, 32), true);
            AddScore(ScoreBest(document.SubCategoryNames, query, 50, 35, 32), true);
            AddScore(ScoreBest(document.TechnicalPowerValues, query, 80, 55, 40), true, 120);
            AddScore(ScoreBest(document.Features, query, 55, 35, 18), true, 65);
            AddScore(ScoreBest(document.DescriptionTexts, query, 45, 35, 12), false, 75);

            if (query.NumericQuery.HasValue
                && HasExactTechnicalPowerMatch(document.TechnicalPowerValues, query.NumericQuery.Value))
            {
                score += 70;
                foreach (var index in query.Terms.Where(x => x.Token.Any(char.IsDigit)).Select(x => x.Index))
                {
                    matchedTerms.Add(index);
                    strongMatchedTerms.Add(index);
                }
            }

            return new ProductRank(
                score,
                exactNameMatch,
                MeetsThreshold(query, score, strongMatchedTerms, matchedTerms, exactNameMatch));
        }

        private static ProductSearchTextScore ScoreBest(
            IEnumerable<string> values,
            ProductSearchQuery query,
            double phraseWeight,
            double allTermsWeight,
            double termWeight)
        {
            return values
                .Select(value => ProductSearchHelper.ScoreText(value, query, phraseWeight, allTermsWeight, termWeight))
                .OrderByDescending(x => x.Score)
                .FirstOrDefault() ?? ProductSearchTextScore.Empty;
        }

        private static IReadOnlyList<string> BuildTechnicalPowerSearchValues(
            IEnumerable<ProductParametr> parametrs)
        {
            var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var modelLabel in parametrs
                         .Select(x => x.ModelLabel?.Trim())
                         .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                values.Add(modelLabel!);
            }
            foreach (var rawValue in parametrs
                         .Select(x => x.TechnicalPower?.Trim())
                         .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                values.Add(rawValue!);
                if (!TryParseWatts(rawValue!, out var watts))
                {
                    continue;
                }

                values.Add($"{FormatPower(watts)} W");
                values.Add($"{FormatPower(watts)}W");
                if (watts >= 1_000)
                {
                    values.Add($"{FormatPower(watts / 1_000m)} kW");
                    values.Add($"{FormatPower(watts / 1_000m)}kW");
                }
            }

            return values.ToList();
        }

        private static bool HasExactTechnicalPowerMatch(
            IEnumerable<string> technicalPowerValues,
            decimal numericQuery)
            => technicalPowerValues.Any(value =>
                Regex.Matches(value, @"\d+(?:[.,]\d+)?")
                    .Select(match => decimal.TryParse(
                        match.Value.Replace(',', '.'),
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var parsed) ? parsed : (decimal?)null)
                    .Any(parsed => parsed == numericQuery));

        private static bool TryParseWatts(string value, out decimal watts)
        {
            watts = 0;
            var match = Regex.Match(
                value,
                @"^\s*(?<value>\d+(?:[.,]\d+)?)\s*(?<unit>kw|w)?\s*$",
                RegexOptions.IgnoreCase);
            if (!match.Success
                || !decimal.TryParse(
                    match.Groups["value"].Value.Replace(',', '.'),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return false;
            }

            watts = match.Groups["unit"].Value.Equals("kw", StringComparison.OrdinalIgnoreCase)
                ? parsed * 1_000m
                : parsed;
            return watts > 0;
        }

        private static decimal? ParseStandalonePowerQueryWatts(string value)
        {
            if (!Regex.IsMatch(
                    value ?? string.Empty,
                    @"^\s*\d+(?:[.,]\d+)?\s*(?:kw|w)\s*$",
                    RegexOptions.IgnoreCase))
            {
                return null;
            }

            return TryParseWatts(value, out var watts) ? watts : null;
        }

        private static bool HasEquivalentTechnicalPower(
            IEnumerable<ProductParametr> parametrs,
            decimal watts)
            => parametrs.Any(x =>
                !string.IsNullOrWhiteSpace(x.TechnicalPower)
                && TryParseWatts(x.TechnicalPower, out var variantWatts)
                && variantWatts == watts);

        private static string FormatPower(decimal value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static bool MeetsThreshold(
            ProductSearchQuery query,
            double score,
            HashSet<int> strongMatchedTerms,
            HashSet<int> matchedTerms,
            bool exactNameMatch)
        {
            if (query.Terms.Count == 0)
            {
                return false;
            }

            if (exactNameMatch || score >= 180)
            {
                return true;
            }

            if (query.IsSingleTerm)
            {
                return score >= 35 && strongMatchedTerms.Count > 0;
            }

            var requiredStrongMatches = Math.Max(2, (int)Math.Ceiling(query.Terms.Count * 0.75));
            if (score >= 50 && strongMatchedTerms.Count >= requiredStrongMatches)
            {
                return true;
            }

            var requiredMeaningfulMatches = Math.Max(2, (int)Math.Ceiling(query.Terms.Count * 0.75));
            return score >= 60 && matchedTerms.Count >= requiredMeaningfulMatches;
        }

        private sealed record ProductSearchCatalog(
            IReadOnlyList<ProductSearchDocument> Documents);

        private sealed record ProductSearchDocument(
            Product Product,
            IReadOnlyList<ProductImage> Images,
            IReadOnlyList<ProductParametr> Parametrs,
            IReadOnlyList<ProductDescription> ProductDescriptions,
            IReadOnlyList<ProductDescriptionLanguage> DescriptionLanguages,
            IReadOnlyList<int> PromotionIds,
            string BrandName,
            string TechnologyName,
            IReadOnlyList<string> CategoryNames,
            IReadOnlyList<string> SubCategoryNames,
            IReadOnlyList<string> TechnicalPowerValues,
            IReadOnlyList<string> DescriptionTexts,
            IReadOnlyList<string> Features);

        private sealed record ProductRank(
            double Score,
            bool ExactNameMatch,
            bool IsMatch);
    }
}
