using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Dtos.About;
using Volt.Application.Dtos.ProdcutCategory;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public class ProductCategoryService : IProductCategoryService
    {
        private readonly IUnitOfWork _uow;

        public ProductCategoryService(IUnitOfWork uow)
        {
            _uow = uow;
        }
        public async Task<ApiResponse<ProductCategoryDto>> CreateAsync(ProductCategoryCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<ProductCategoryDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                var ProductCategoryRepo = _uow.Repository<ProductCategory>();

                var productCategory = new ProductCategory
                {
                    IsActive = true,
                    ShowOnHomePage = request.ShowOnHomePage,
                    HomePageDisplayOrder = request.ShowOnHomePage ? request.HomePageDisplayOrder : 0,
                    HomePageProductId = null,
                };

                if (request.ShowOnHomePage)
                {
                    var presentationValidation = await ValidateHomePagePresentationAsync(null, request.HomePageDisplayOrder, ct);
                    if (presentationValidation is not null)
                    {
                        return ApiResponse<ProductCategoryDto>.ErrorResponse(
                            ErrorCode.INVALID_PRODUCT_CATEGORY_REQUEST,
                            presentationValidation);
                    }
                }

                await ProductCategoryRepo.AddAsync(productCategory, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var item in request.Languages)
                {
                    var language = new ProductCategoryLanguage
                    {
                        ProductCategoryId = productCategory.Id,
                        LanguageCode = item.LanguageCode,
                        CategoryName = item.CategoryName,
                        IsActive = true
                    };

                    await _uow.Repository<ProductCategoryLanguage>().AddAsync(language, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(productCategory.Id, null, ct);
                return ApiResponse<ProductCategoryDto>.SuccessResponse(dto);
            }
            catch (Exception ex)
            {

                return ApiResponse<ProductCategoryDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "Server Side Error");
            }
        }
        public async Task<ApiResponse<IReadOnlyList<ProductCategoryDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var productCategories = await _uow.Repository<ProductCategory>().ListNoTrackingAsync(x => x.IsActive, ct);
            var languages = await _uow.Repository<ProductCategoryLanguage>().ListNoTrackingAsync(ct);

            var results = productCategories
                .OrderBy(x => x.Id)
                .Select(x => MapToDto(
                    x,
                    languages.Where(l => l.ProductCategoryId == x.Id).OrderBy(l => l.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<ProductCategoryDto>>.SuccessResponse(results);
        }
        public async Task<ApiResponse<ProductCategoryDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var productCategory = await _uow.Repository<ProductCategory>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (productCategory is null)
            {
                return ApiResponse<ProductCategoryDto>.ErrorResponse(
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND);
            }

            var dto = await BuildDtoAsync(id, null, ct);
            return ApiResponse<ProductCategoryDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<ProductCategoryDto>> GetBySeoKeyAsync(
            string seoKey,
            LanguageCode? languageCode = null,
            CancellationToken ct = default)
        {
            var normalizedKey = seoKey?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return ApiResponse<ProductCategoryDto>.ErrorResponse(
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND);
            }

            var productCategory = await _uow.Repository<ProductCategory>().FirstOrDefaultNoTrackingAsync(
                x => x.IsActive && x.SeoKey == normalizedKey,
                ct);
            if (productCategory is null)
            {
                return ApiResponse<ProductCategoryDto>.ErrorResponse(
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND);
            }

            var languages = await _uow.Repository<ProductCategoryLanguage>().ListNoTrackingAsync(
                x => x.ProductCategoryId == productCategory.Id && x.IsActive,
                ct);
            return ApiResponse<ProductCategoryDto>.SuccessResponse(
                MapToDto(productCategory, languages.OrderBy(x => x.Id), languageCode));
        }
        public async Task<ApiResponse<ProductCategoryDto>> UpdateAsync(int id, ProductCategoryUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var productCategoryRepo = _uow.Repository<ProductCategory>();

            var productCategory = await productCategoryRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (productCategory is null)
            {
                return ApiResponse<ProductCategoryDto>.ErrorResponse(
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<ProductCategoryDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                if (request.ShowOnHomePage)
                {
                    var presentationValidation = await ValidateHomePagePresentationAsync(id, request.HomePageDisplayOrder, ct);
                    if (presentationValidation is not null)
                    {
                        return ApiResponse<ProductCategoryDto>.ErrorResponse(
                            ErrorCode.INVALID_PRODUCT_CATEGORY_REQUEST,
                            presentationValidation);
                    }
                }

                var homePageProductId = request.HomePageProductId;
                if (homePageProductId.HasValue)
                {
                    var validProduct = await _uow.Repository<Product>().AnyAsync(
                        x => x.Id == homePageProductId.Value && x.ProductCategoryId == id && x.IsActive,
                        ct);
                    if (!validProduct)
                    {
                        return ApiResponse<ProductCategoryDto>.ErrorResponse(
                            ErrorCode.INVALID_PRODUCT_CATEGORY_REQUEST,
                            "The selected homepage product does not belong to this category.");
                    }
                }
                else if (request.ShowOnHomePage)
                {
                    var availableProducts = await _uow.Repository<Product>().ListNoTrackingAsync(
                        x => x.ProductCategoryId == id && x.IsActive,
                        ct);
                    homePageProductId = availableProducts.OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefault();
                }

                productCategory.ShowOnHomePage = request.ShowOnHomePage;
                productCategory.HomePageDisplayOrder = request.ShowOnHomePage ? request.HomePageDisplayOrder : 0;
                productCategory.HomePageProductId = homePageProductId;
                productCategoryRepo.Update(productCategory);

                await UpsertLanguagesAsync(id, request.Languages, ct);

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(id, languageCode, ct);
                return ApiResponse<ProductCategoryDto>.SuccessResponse(dto);

            }
            catch (Exception)
            {

                return ApiResponse<ProductCategoryDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the about record.");
            }
        }
        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var productCategoryRepo = _uow.Repository<ProductCategory>();
            var productCategory = await productCategoryRepo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (productCategory is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND);
            }

            try
            {
                productCategory.IsActive = false;
                productCategoryRepo.Update(productCategory);

                 var languageRepo = _uow.Repository<ProductCategoryLanguage>();

                var languages = await languageRepo.ListNoTrackingAsync(x => x.ProductCategoryId == id, ct);
                foreach (var language in languages)
                {
                    var trackedLanguage = await languageRepo.FirstOrDefaultAsync(x => x.Id == language.Id, ct);
                    if (trackedLanguage is not null)
                    {
                        trackedLanguage.IsActive = false;
                        languageRepo.Update(trackedLanguage);
                    }
                }

                await _uow.SaveChangesAsync(ct);

                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the about record.");
            }
        }

        public async Task<ApiResponse<IReadOnlyList<HomePageProductCategoryDto>>> GetHomePageAsync(
            LanguageCode? languageCode = null,
            CancellationToken ct = default)
        {
            var categories = (await _uow.Repository<ProductCategory>().ListNoTrackingAsync(
                    x => x.IsActive && x.ShowOnHomePage,
                    ct))
                .OrderBy(x => x.HomePageDisplayOrder)
                .ThenBy(x => x.Id)
                .Take(5)
                .ToList();

            if (categories.Count == 0)
            {
                return ApiResponse<IReadOnlyList<HomePageProductCategoryDto>>.SuccessResponse(
                    Array.Empty<HomePageProductCategoryDto>());
            }

            var categoryIds = categories.Select(x => x.Id).ToList();
            var products = await _uow.Repository<Product>().ListNoTrackingAsync(
                x => x.IsActive && categoryIds.Contains(x.ProductCategoryId),
                ct);
            var productIds = products.Select(x => x.Id).ToList();
            var images = productIds.Count == 0
                ? new List<ProductImage>()
                : await _uow.Repository<ProductImage>().ListNoTrackingAsync(
                    x => x.Type && productIds.Contains(x.ProductId),
                    ct);
            var languages = await _uow.Repository<ProductCategoryLanguage>().ListNoTrackingAsync(
                x => x.IsActive && categoryIds.Contains(x.ProductCategoryId),
                ct);

            var firstImages = images
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.OrderBy(i => i.Id).First().ImageUrl);

            var results = new List<HomePageProductCategoryDto>(categories.Count);
            foreach (var category in categories)
            {
                var categoryProducts = products
                    .Where(x => x.ProductCategoryId == category.Id)
                    .OrderBy(x => x.Id)
                    .ToList();
                var product = categoryProducts.FirstOrDefault(
                        x => x.Id == category.HomePageProductId && firstImages.ContainsKey(x.Id))
                    ?? categoryProducts.FirstOrDefault(x => firstImages.ContainsKey(x.Id))
                    ?? categoryProducts.FirstOrDefault();

                if (product is null)
                {
                    continue;
                }

                var categoryLanguages = languages.Where(x => x.ProductCategoryId == category.Id).ToList();
                var name = categoryLanguages.FirstOrDefault(x => x.LanguageCode == languageCode)?.CategoryName
                    ?? categoryLanguages.FirstOrDefault(x => x.LanguageCode == LanguageCode.AZ)?.CategoryName
                    ?? categoryLanguages.FirstOrDefault()?.CategoryName
                    ?? string.Empty;

                results.Add(new HomePageProductCategoryDto(
                    category.Id,
                    category.SeoKey,
                    name,
                    product.Id,
                    product.ProductName,
                    firstImages.GetValueOrDefault(product.Id, string.Empty),
                    category.HomePageDisplayOrder));
            }

            return ApiResponse<IReadOnlyList<HomePageProductCategoryDto>>.SuccessResponse(results);
        }

        public async Task<ApiResponse<IReadOnlyList<ProductCategoryProductOptionDto>>> GetProductOptionsAsync(
            int id,
            CancellationToken ct = default)
        {
            var categoryExists = await _uow.Repository<ProductCategory>().AnyAsync(x => x.Id == id && x.IsActive, ct);
            if (!categoryExists)
            {
                return ApiResponse<IReadOnlyList<ProductCategoryProductOptionDto>>.ErrorResponse(
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_CATEGORY_NOT_FOUND);
            }

            var products = await _uow.Repository<Product>().ListNoTrackingAsync(
                x => x.ProductCategoryId == id && x.IsActive,
                ct);
            var productIds = products.Select(x => x.Id).ToList();
            var images = productIds.Count == 0
                ? new List<ProductImage>()
                : await _uow.Repository<ProductImage>().ListNoTrackingAsync(
                    x => x.Type && productIds.Contains(x.ProductId),
                    ct);
            var firstImages = images
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.OrderBy(i => i.Id).First().ImageUrl);

            var results = products
                .OrderBy(x => x.ProductName)
                .ThenBy(x => x.Id)
                .Where(x => firstImages.ContainsKey(x.Id))
                .Select(x => new ProductCategoryProductOptionDto(
                    x.Id,
                    x.ProductName,
                    firstImages.GetValueOrDefault(x.Id, string.Empty)))
                .ToList();

            return ApiResponse<IReadOnlyList<ProductCategoryProductOptionDto>>.SuccessResponse(results);
        }
        private string? ValidateLanguages(List<LanguageCode> languages)
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_PRODUCT_CATEGORY_REQUEST;
            }

            if (languages.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.PRODUCT_CATEGORY_LANGUAGE_DUPLICATE;
            }

            return null;
        }

        private async Task<string?> ValidateHomePagePresentationAsync(int? categoryId, int displayOrder, CancellationToken ct)
        {
            if (displayOrder is < 1 or > 5)
            {
                return "Homepage display order must be between 1 and 5.";
            }

            var visibleCount = await _uow.Repository<ProductCategory>().CountAsync(
                x => x.IsActive && x.ShowOnHomePage && (!categoryId.HasValue || x.Id != categoryId.Value),
                ct);

            return visibleCount >= 5
                ? "A maximum of five product categories can be shown on the homepage."
                : null;
        }
        private async Task UpsertLanguagesAsync(int productCategoryId, List<ProductCategoryLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var languageRepo = _uow.Repository<ProductCategoryLanguage>();
            var existingLanguages = await languageRepo.ListNoTrackingAsync(x => x.ProductCategoryId == productCategoryId, ct);

            foreach (var item in languages)
            {
                var existingLanguage = existingLanguages.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);

                if (existingLanguage is null)
                {
                    var newLanguage = new ProductCategoryLanguage
                    {
                        ProductCategoryId = productCategoryId,
                        LanguageCode = item.LanguageCode,
                        CategoryName = item.CategoryName,
                    };

                    await languageRepo.AddAsync(newLanguage, ct);
                }
                else
                {
                    var trackedLanguage = await languageRepo.FirstOrDefaultAsync(x => x.Id == existingLanguage.Id, ct);
                    if (trackedLanguage is not null)
                    {
                        trackedLanguage.CategoryName = item.CategoryName;
                        languageRepo.Update(trackedLanguage);
                    }
                }
            }
        }

        private async Task<ProductCategoryDto> BuildDtoAsync(int productCategoryid, LanguageCode? languageCode, CancellationToken ct)
        {
            var productCategory = await _uow.Repository<ProductCategory>().FirstOrDefaultNoTrackingAsync(x => x.Id == productCategoryid, ct);
            var languages = await _uow.Repository<ProductCategoryLanguage>().ListNoTrackingAsync(x => x.ProductCategoryId
            == productCategoryid, ct);

            return MapToDto(
                productCategory,
                languages.OrderBy(x => x.Id).ToList(),
                languageCode
                );
        }
        
        private ProductCategoryDto MapToDto(
            ProductCategory productCategory,
            IEnumerable<ProductCategoryLanguage> languages,
            LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode);

            return new ProductCategoryDto(
                productCategory.Id,
                productCategory.SeoKey,
                filteredLanguages.Select(x => new ProductCategoryLanguageDto(
                    x.LanguageCode,
                    x.CategoryName
                    )).ToList()
                    .ToList(),
                productCategory.ShowOnHomePage,
                productCategory.HomePageDisplayOrder,
                productCategory.HomePageProductId);
                
        }

        
    }
}
