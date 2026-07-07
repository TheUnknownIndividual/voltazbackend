using Volt.Domain.Entities;

namespace Volt.Application.Interfaces
{
    public interface IProductSearchService
    {
        Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
            string query,
            int? categoryId = null,
            int? subCategoryId = null,
            CancellationToken ct = default);

        void Invalidate();
    }

    public sealed record ProductSearchResult(
        Product Product,
        IReadOnlyList<ProductImage> Images,
        IReadOnlyList<ProductParametr> Parametrs,
        IReadOnlyList<ProductDescription> Descriptions,
        IReadOnlyList<ProductDescriptionLanguage> DescriptionLanguages,
        IReadOnlyList<int> PromotionIds,
        double Score,
        bool ExactNameMatch);
}
