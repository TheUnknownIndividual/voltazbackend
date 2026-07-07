using Volt.Application.Dtos;
using Volt.Application.Dtos.Product;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ProductService : IProductService
    {
        private readonly IUnitOfWork _uow;
        private readonly IProductSearchService _productSearchService;
        private readonly ISeoSubmissionQueue _seoSubmissionQueue;

        public ProductService(
            IUnitOfWork uow,
            IProductSearchService productSearchService,
            ISeoSubmissionQueue seoSubmissionQueue)
        {
            _uow = uow;
            _productSearchService = productSearchService;
            _seoSubmissionQueue = seoSubmissionQueue;
        }

        public async Task<ApiResponse<ProductDto>> CreateAsync(ProductCreateRequest request, CancellationToken ct = default)
        {
            var validationError = await ValidateReferencesAsync(
                request.ProductCategoryId,
                request.ProductSubCategoryId,
                request.ProductBrandId,
                request.ProductTechnologyId,
                ct);
            if (validationError is not null)
            {
                return ApiResponse<ProductDto>.ErrorResponse(validationError, validationError);
            }

            var promotionValidation = await ValidatePromotionIdsAsync(request.PromotionIds, ct);
            if (promotionValidation is not null)
            {
                return ApiResponse<ProductDto>.ErrorResponse(promotionValidation, promotionValidation);
            }

            var descriptionLanguagesValidation = ValidateProductDescriptionLanguages(request.ProductDescriptions);
            if (descriptionLanguagesValidation is not null)
            {
                return ApiResponse<ProductDto>.ErrorResponse(descriptionLanguagesValidation, descriptionLanguagesValidation);
            }

            try
            {
                var hasAvailableStock = HasAvailableStock(request.ProductParametrs);
                var product = new Product
                {
                    ProductName = request.ProductName.Trim(),
                    ProductCategoryId = request.ProductCategoryId,
                    ProductSubCategoryId = request.ProductSubCategoryId,
                    ProductBrandId = request.ProductBrandId,
                    ProductTechnologyId = request.ProductTechnologyId,
                    InStock = hasAvailableStock,
                    OutOfStockAt = hasAvailableStock ? null : DateTime.UtcNow,
                    InHomePage = request.InHomePage,
                    Certificate = request.Certificate,
                    IsActive = true
                };

                await _uow.Repository<Product>().AddAsync(product, ct);
                await _uow.SaveChangesAsync(ct);

                await ReplaceProductImagesAsync(product.Id, request.ProductImage, request.ProductDatasheet, ct);
                await ReplaceProductParametrsAsync(product.Id, request.ProductParametrs, ct);
                await ReplaceProductDescriptionsAsync(product.Id, request.ProductDescriptions, ct);
                await ReplaceProductPromotionsAsync(product.Id, request.PromotionIds, ct);
                await _uow.SaveChangesAsync(ct);
                _productSearchService.Invalidate();

                var dto = await BuildDtoAsync(product.Id, ct);
                _seoSubmissionQueue.EnqueueProductCreated(product.Id);
                return ApiResponse<ProductDto>.SuccessResponse(dto);
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductDto>.ErrorResponse(ErrorCode.SERVER_ERROR, ex.Message);
            }
        }

        public async Task<ApiResponse<PagedResult<ProductDto>>> GetAllForHomePageAsync(ProductFiltrHomePageDto dto, CancellationToken ct = default)
        {
            var page = dto?.Page > 0 ? dto.Page : 1;
            var pageSize = dto?.PageSize > 0 ? Math.Min(dto.PageSize, 100) : 10;

            var allProducts = await _uow.Repository<Product>().ListNoTrackingAsync(x => x.IsActive && x.InHomePage, ct);
            if (allProducts.Count == 0)
            {
                return ApiResponse<PagedResult<ProductDto>>.SuccessResponse(new PagedResult<ProductDto>
                {
                    Items = Array.Empty<ProductDto>(),
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 0
                });

            }

            var productIds = allProducts.Select(x => x.Id).ToHashSet();
            var allImages = await _uow.Repository<ProductImage>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId), ct);
            var allParametrs = await _uow.Repository<ProductParametr>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId) && x.IsActive, ct);
            var allDescriptions = await _uow.Repository<ProductDescription>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId), ct);
            var descriptionIds = allDescriptions.Select(x => x.Id).ToHashSet();
            var allDescriptionLanguages = descriptionIds.Count == 0
                ? new List<ProductDescriptionLanguage>()
                : await _uow.Repository<ProductDescriptionLanguage>().ListNoTrackingAsync(
                    x => descriptionIds.Contains(x.ProductDescriptionId) && x.IsActive, ct);
            var promotionIdsByProduct = await GetActivePromotionIdsByProductAsync(productIds, ct);

            var products = allProducts
                .OrderByDescending(x => HasPurchasableVariant(x.Id, allParametrs))
                .ThenByDescending(x => HasVisiblePrice(x.Id, allParametrs))
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = products
                .Select(x => MapToDto(
                    x,
                    allImages.Where(i => i.ProductId == x.Id),
                    allParametrs.Where(p => p.ProductId == x.Id),
                    allDescriptions.Where(d => d.ProductId == x.Id),
                    allDescriptionLanguages,
                    promotionIdsByProduct.GetValueOrDefault(x.Id, [])))
                .ToList();

            return ApiResponse<PagedResult<ProductDto>>.SuccessResponse(new PagedResult<ProductDto>
            {
                Items = result,
                Page = page,
                PageSize = pageSize,
                TotalCount = allProducts.Count,
                TotalPages = (int)Math.Ceiling(allProducts.Count / (double)pageSize)
            });

        }
        public async Task<ApiResponse<PagedResult<ProductDto>>> GetAllAsync(ProductFiltrDto dto = null, CancellationToken ct = default)
        {
            int? categoryId = dto?.ProductCategoryId;
            int? subCategoryId = dto?.ProductSubCategoryId;
            var page = dto?.Page > 0 ? dto.Page : 1;
            var pageSize = dto?.PageSize > 0 ? Math.Min(dto.PageSize, 100) : 10;
            var search = ProductSearchHelper.Normalize(dto?.Search);

            if (!string.IsNullOrWhiteSpace(search))
            {
                return await GetAllBySearchAsync(categoryId, subCategoryId, search, page, pageSize, dto?.StockStatus ?? ProductStockStatus.All, ct);
            }

            var productRepo = _uow.Repository<Product>();

            var allProducts = await productRepo.ListNoTrackingAsync(
                x => x.IsActive
                    && (categoryId == null || x.ProductCategoryId == categoryId)
                    && (subCategoryId == null || x.ProductSubCategoryId == subCategoryId),
                ct);

            if (allProducts.Count == 0)
            {
                return ApiResponse<PagedResult<ProductDto>>.SuccessResponse(new PagedResult<ProductDto>
                {
                    Items = Array.Empty<ProductDto>(),
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 0
                });
            }

            var productIds = allProducts.Select(x => x.Id).ToHashSet();
            var allImages = await _uow.Repository<ProductImage>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId), ct);
            var allParametrs = await _uow.Repository<ProductParametr>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId) && x.IsActive, ct);
            var allDescriptions = await _uow.Repository<ProductDescription>().ListNoTrackingAsync(x => productIds.Contains(x.ProductId), ct);
            var descriptionIds = allDescriptions.Select(x => x.Id).ToHashSet();
            var allDescriptionLanguages = descriptionIds.Count == 0
                ? new List<ProductDescriptionLanguage>()
                : await _uow.Repository<ProductDescriptionLanguage>().ListNoTrackingAsync(
                    x => descriptionIds.Contains(x.ProductDescriptionId) && x.IsActive, ct);
            var promotionIdsByProduct = await GetActivePromotionIdsByProductAsync(productIds, ct);

            var stockStatus = dto?.StockStatus ?? ProductStockStatus.All;
            var products = allProducts
                .Where(x => MatchesStockStatus(x, allParametrs, stockStatus))
                .OrderByDescending(x => stockStatus == ProductStockStatus.OutOfStock ? x.OutOfStockAt ?? DateTime.MinValue : DateTime.MinValue)
                .ThenByDescending(x => HasPurchasableVariant(x.Id, allParametrs))
                .ThenByDescending(x => HasVisiblePrice(x.Id, allParametrs))
                .ThenByDescending(x => x.Id)
                .ToList();

            var totalCount = products.Count;
            var pagedProducts = products
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = pagedProducts
                .Select(x => MapToDto(
                    x,
                    allImages.Where(i => i.ProductId == x.Id),
                    allParametrs.Where(p => p.ProductId == x.Id),
                    allDescriptions.Where(d => d.ProductId == x.Id),
                    allDescriptionLanguages,
                    promotionIdsByProduct.GetValueOrDefault(x.Id, [])))
                .ToList();

            return ApiResponse<PagedResult<ProductDto>>.SuccessResponse(new PagedResult<ProductDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }  

        private static bool HasVisiblePrice(int productId, IEnumerable<ProductParametr> parametrs)
            => parametrs.Any(x => x.ProductId == productId && x.Amount > 0);

        private static bool HasPurchasableVariant(int productId, IEnumerable<ProductParametr> parametrs)
            => parametrs.Any(x => x.ProductId == productId && x.IsActive && x.Count.GetValueOrDefault() > 0 && x.Amount.GetValueOrDefault() > 0);

        private static bool HasAvailableStock(IEnumerable<ProductParametrCreateRequest> parametrs)
            => parametrs?.Any(x => x.Count > 0) == true;

        private static bool ProductHasAvailableStock(int productId, IEnumerable<ProductParametr> parametrs)
            => parametrs.Any(x => x.ProductId == productId && x.IsActive && x.Count.GetValueOrDefault() > 0);

        private static bool MatchesStockStatus(Product product, IEnumerable<ProductParametr> parametrs, ProductStockStatus status)
        {
            if (status == ProductStockStatus.All)
            {
                return true;
            }

            var available = product.InStock && ProductHasAvailableStock(product.Id, parametrs);
            return status == ProductStockStatus.InStock ? available : !available;
        }

        private async Task<ApiResponse<PagedResult<ProductDto>>> GetAllBySearchAsync(
            int? categoryId,
            int? subCategoryId,
            string search,
            int page,
            int pageSize,
            ProductStockStatus stockStatus,
            CancellationToken ct)
        {
            var matchedProducts = await _productSearchService.SearchAsync(search, categoryId, subCategoryId, ct);
            matchedProducts = matchedProducts
                .Where(x => MatchesStockStatus(x.Product, x.Parametrs, stockStatus))
                .ToList();

            if (stockStatus == ProductStockStatus.OutOfStock)
            {
                matchedProducts = matchedProducts
                    .OrderByDescending(x => x.Product.OutOfStockAt ?? DateTime.MinValue)
                    .ThenByDescending(x => x.Product.Id)
                    .ToList();
            }

            var totalCount = matchedProducts.Count;
            if (totalCount == 0)
            {
                return ApiResponse<PagedResult<ProductDto>>.SuccessResponse(new PagedResult<ProductDto>
                {
                    Items = Array.Empty<ProductDto>(),
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 0
                });
            }

            var pagedProducts = matchedProducts
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = pagedProducts
                .Select(x => MapToDto(
                    x.Product,
                    x.Images,
                    x.Parametrs,
                    x.Descriptions,
                    x.DescriptionLanguages,
                    x.PromotionIds))
                .ToList();

            return ApiResponse<PagedResult<ProductDto>>.SuccessResponse(new PagedResult<ProductDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        public async Task<ApiResponse<ProductDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var product = await _uow.Repository<Product>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);
            if (product is null)
            {
                return ApiResponse<ProductDto>.ErrorResponse(ErrorCode.PRODUCT_NOT_FOUND, ErrorCode.PRODUCT_NOT_FOUND);
            }

            return ApiResponse<ProductDto>.SuccessResponse(await BuildDtoAsync(id, ct));
        }

        public async Task<ApiResponse<ProductDto>> UpdateAsync(int id, ProductUpdateRequest request, CancellationToken ct = default)
        {
            var productRepo = _uow.Repository<Product>();
            var product = await productRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (product is null)
            {
                return ApiResponse<ProductDto>.ErrorResponse(ErrorCode.PRODUCT_NOT_FOUND, ErrorCode.PRODUCT_NOT_FOUND);
            }

            var validationError = await ValidateReferencesAsync(
                request.ProductCategoryId,
                request.ProductSubCategoryId,
                request.ProductBrandId,
                request.ProductTechnologyId,
                ct);
            if (validationError is not null)
            {
                return ApiResponse<ProductDto>.ErrorResponse(validationError, validationError);
            }

            var promotionValidation = await ValidatePromotionIdsAsync(request.PromotionIds, ct);
            if (promotionValidation is not null)
            {
                return ApiResponse<ProductDto>.ErrorResponse(promotionValidation, promotionValidation);
            }

            var descriptionLanguagesValidation = ValidateProductDescriptionLanguages(request.ProductDescriptions);
            if (descriptionLanguagesValidation is not null)
            {
                return ApiResponse<ProductDto>.ErrorResponse(descriptionLanguagesValidation, descriptionLanguagesValidation);
            }

            try
            {
                var previousInStock = product.InStock;
                var hasAvailableStock = HasAvailableStock(request.ProductParametrs);
                product.ProductName = request.ProductName.Trim();
                product.ProductCategoryId = request.ProductCategoryId;
                product.ProductSubCategoryId = request.ProductSubCategoryId;
                product.ProductBrandId = request.ProductBrandId;
                product.ProductTechnologyId = request.ProductTechnologyId;
                product.InStock = hasAvailableStock;
                if (!hasAvailableStock && previousInStock)
                {
                    product.OutOfStockAt = DateTime.UtcNow;
                }
                if (hasAvailableStock)
                {
                    product.OutOfStockAt = null;
                }
                product.InHomePage = request.InHomePage;
                product.Certificate = request.Certificate;
                productRepo.Update(product);

                await ReplaceProductImagesAsync(id, request.ProductImage, request.ProductDatasheet, ct);
                await ReplaceProductParametrsAsync(id, request.ProductParametrs, ct);
                await ReplaceProductDescriptionsAsync(id, request.ProductDescriptions, ct);
                await ReplaceProductPromotionsAsync(id, request.PromotionIds, ct);
                await _uow.SaveChangesAsync(ct);
                _productSearchService.Invalidate();

                var dto = await BuildDtoAsync(id, ct);
                return ApiResponse<ProductDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<ProductDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occurred while updating the product.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var productRepo = _uow.Repository<Product>();
            var product = await productRepo.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (product is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.PRODUCT_NOT_FOUND, ErrorCode.PRODUCT_NOT_FOUND);
            }

            try
            {
                product.IsActive = false;
                productRepo.Update(product);
                await UnlinkProductPromotionsAsync(id, ct);
                await _uow.SaveChangesAsync(ct);
                _productSearchService.Invalidate();
                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occurred while deleting the product.");
            }
        }

        private async Task<ProductDto> BuildDtoAsync(int productId, CancellationToken ct)
        {
            var product = await _uow.Repository<Product>().FirstOrDefaultNoTrackingAsync(x => x.Id == productId && x.IsActive, ct);
            var images = await _uow.Repository<ProductImage>().ListNoTrackingAsync(x => x.ProductId == productId, ct);
            var parametrs = await _uow.Repository<ProductParametr>().ListNoTrackingAsync(x => x.ProductId == productId && x.IsActive, ct);
            var descriptions = await _uow.Repository<ProductDescription>().ListNoTrackingAsync(x => x.ProductId == productId, ct);
            var descriptionIds = descriptions.Select(d => d.Id).ToHashSet();
            var descriptionLanguages = descriptionIds.Count == 0
                ? new List<ProductDescriptionLanguage>()
                : await _uow.Repository<ProductDescriptionLanguage>().ListNoTrackingAsync(
                    x => descriptionIds.Contains(x.ProductDescriptionId) && x.IsActive, ct);
            var promotionIds = await GetActivePromotionIdsForProductAsync(productId, ct);

            return MapToDto(product, images, parametrs, descriptions, descriptionLanguages, promotionIds);
        }

        private async Task ReplaceProductImagesAsync(
            int productId,
            List<string>? productImages,
            List<string>? productDatasheets,
            CancellationToken ct)
        {
            var imageRepo = _uow.Repository<ProductImage>();
            var existingImages = await imageRepo.ListNoTrackingAsync(x => x.ProductId == productId, ct);

            foreach (var existing in existingImages)
            {
                var tracked = await imageRepo.FirstOrDefaultAsync(x => x.Id == existing.Id, ct);
                if (tracked is not null)
                {
                    imageRepo.Remove(tracked);
                }
            }

            foreach (var imageUrl in (productImages ?? []).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                await imageRepo.AddAsync(new ProductImage
                {
                    ProductId = productId,
                    ImageUrl = imageUrl.Trim(),
                    Type = true
                }, ct);
            }

            foreach (var imageUrl in (productDatasheets ?? []).Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                await imageRepo.AddAsync(new ProductImage
                {
                    ProductId = productId,
                    ImageUrl = imageUrl.Trim(),
                    Type = false
                }, ct);
            }
        }

        private async Task ReplaceProductParametrsAsync(
            int productId,
            List<ProductParametrCreateRequest>? requestParametrs,
            CancellationToken ct)
        {
            var parametrRepo = _uow.Repository<ProductParametr>();
            var existingParametrs = await parametrRepo.ListNoTrackingAsync(x => x.ProductId == productId, ct);

            foreach (var existing in existingParametrs)
            {
                var tracked = await parametrRepo.FirstOrDefaultAsync(x => x.Id == existing.Id, ct);
                if (tracked is not null)
                {
                    parametrRepo.Remove(tracked);
                }
            }

            foreach (var item in requestParametrs ?? [])
            {
                await parametrRepo.AddAsync(new ProductParametr
                {
                    ProductId = productId,
                    TechnicalPower = item.TechnicalPower,
                    Effectiveness = item.Effectiveness,
                    Count = item.Count,
                    Amount = item.Amount,
                    IsActive = true
                }, ct);
            }
        }

        private async Task ReplaceProductDescriptionsAsync(
            int productId,
            List<ProductDescriptionCreateRequest>? requestDescriptions,
            CancellationToken ct)
        {
            var descriptionRepo = _uow.Repository<ProductDescription>();
            var languageRepo = _uow.Repository<ProductDescriptionLanguage>();

            var existingDescriptions = await descriptionRepo.ListNoTrackingAsync(x => x.ProductId == productId, ct);

            foreach (var existingDescription in existingDescriptions)
            {
                var existingLanguages = await languageRepo.ListNoTrackingAsync(
                    x => x.ProductDescriptionId == existingDescription.Id, ct);
                foreach (var existingLanguage in existingLanguages)
                {
                    var trackedLanguage = await languageRepo.FirstOrDefaultAsync(x => x.Id == existingLanguage.Id, ct);
                    if (trackedLanguage is not null)
                    {
                        languageRepo.Remove(trackedLanguage);
                    }
                }

                var trackedDescription = await descriptionRepo.FirstOrDefaultAsync(x => x.Id == existingDescription.Id, ct);
                if (trackedDescription is not null)
                {
                    descriptionRepo.Remove(trackedDescription);
                }
            }

            foreach (var requestDescription in requestDescriptions ?? [])
            {
                var description = new ProductDescription { ProductId = productId };
                await descriptionRepo.AddAsync(description, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var language in requestDescription.Languages)
                {
                    await languageRepo.AddAsync(new ProductDescriptionLanguage
                    {
                        ProductDescriptionId = description.Id,
                        LanguageCode = language.LanguageCode,
                        Description = language.Description,
                        Features = language.Features,
                        IsActive = true
                    }, ct);
                }
            }
        }

        private async Task ReplaceProductPromotionsAsync(int productId, List<int>? promotionIds, CancellationToken ct)
        {
            var linkRepo = _uow.Repository<ProductPromotion>();
            var requestedIds = (promotionIds ?? []).Distinct().ToHashSet();

            var currentlyLinked = await linkRepo.ListNoTrackingAsync(x => x.ProductId == productId, ct);
            foreach (var link in currentlyLinked)
            {
                if (requestedIds.Contains(link.PromotionId))
                {
                    continue;
                }

                var tracked = await linkRepo.FirstOrDefaultAsync(
                    x => x.ProductId == productId && x.PromotionId == link.PromotionId, ct);
                if (tracked is not null)
                {
                    linkRepo.Remove(tracked);
                }
            }

            var existingIds = currentlyLinked.Select(x => x.PromotionId).ToHashSet();
            foreach (var promotionId in requestedIds.Where(id => !existingIds.Contains(id)))
            {
                await linkRepo.AddAsync(new ProductPromotion
                {
                    ProductId = productId,
                    PromotionId = promotionId
                }, ct);
            }
        }

        private async Task UnlinkProductPromotionsAsync(int productId, CancellationToken ct)
        {
            var linkRepo = _uow.Repository<ProductPromotion>();
            var linked = await linkRepo.ListNoTrackingAsync(x => x.ProductId == productId, ct);

            foreach (var link in linked)
            {
                var tracked = await linkRepo.FirstOrDefaultAsync(
                    x => x.ProductId == productId && x.PromotionId == link.PromotionId, ct);
                if (tracked is not null)
                {
                    linkRepo.Remove(tracked);
                }
            }
        }

        private async Task<IReadOnlyList<int>> GetActivePromotionIdsForProductAsync(int productId, CancellationToken ct)
        {
            var links = await _uow.Repository<ProductPromotion>().ListNoTrackingAsync(x => x.ProductId == productId, ct);
            if (links.Count == 0)
            {
                return [];
            }

            var promotionIds = links.Select(x => x.PromotionId).ToHashSet();
            var activePromotions = await _uow.Repository<Promotion>().ListNoTrackingAsync(
                x => promotionIds.Contains(x.Id) && x.IsActive, ct);

            return activePromotions.Select(x => x.Id).OrderBy(x => x).ToList();
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

        private async Task<string?> ValidateReferencesAsync(
            int categoryId,
            int subCategoryId,
            int brandId,
            int? technologyId,
            CancellationToken ct)
        {
            var categoryExists = await _uow.Repository<ProductCategory>().AnyAsync(x => x.Id == categoryId && x.IsActive, ct);
            if (!categoryExists)
            {
                return ErrorCode.PRODUCT_CATEGORY_NOT_FOUND;
            }

            var subCategoryExists = await _uow.Repository<ProductSubCategory>().AnyAsync(
                x => x.Id == subCategoryId && x.ProductCategoryId == categoryId && x.IsActive, ct);
            if (!subCategoryExists)
            {
                return ErrorCode.PRODUCT_SUBCATEGORY_NOT_FOUND;
            }

            var brandExists = await _uow.Repository<ProductBrand>().AnyAsync(
                x => x.Id == brandId && x.ProductCategoryId == categoryId && x.IsActive, ct);
            if (!brandExists)
            {
                return ErrorCode.PRODUCT_BRAND_NOT_FOUND;
            }

            if (technologyId.HasValue)
            {
                var technologyExists = await _uow.Repository<ProductTechnology>().AnyAsync(
                    x => x.Id == technologyId.Value && x.ProductCategoryId == categoryId && x.IsActive, ct);
                if (!technologyExists)
                {
                    return ErrorCode.PRODUCT_TECHNOLOGY_NOT_FOUND;
                }
            }

            return null;
        }

        private static string? ValidateProductDescriptionLanguages(List<ProductDescriptionCreateRequest>? descriptions)
        {
            foreach (var description in descriptions ?? [])
            {
                var languageValidation = ValidateDescriptionLanguages(
                    description.Languages.Select(x => x.LanguageCode).ToList());
                if (languageValidation is not null)
                {
                    return languageValidation;
                }
            }

            return null;
        }

        private static string? ValidateDescriptionLanguages(List<LanguageCode> languages)
        {
            if (languages is null || languages.Count == 0)
            {
                return ErrorCode.INVALID_PRODUCT_REQUEST;
            }

            if (languages.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.PRODUCT_DESCRIPTION_LANGUAGE_DUPLICATE;
            }

            return null;
        }

        private async Task<string?> ValidatePromotionIdsAsync(List<int>? promotionIds, CancellationToken ct)
        {
            var distinctIds = (promotionIds ?? []).Distinct().ToList();
            if (distinctIds.Count == 0)
            {
                return null;
            }

            var promotions = await _uow.Repository<Promotion>().ListNoTrackingAsync(x => distinctIds.Contains(x.Id), ct);
            if (promotions.Count != distinctIds.Count)
            {
                return ErrorCode.PROMOTION_NOT_FOUND;
            }

            if (promotions.Any(x => !x.IsActive))
            {
                return ErrorCode.PROMOTION_NOT_FOUND;
            }

            return null;
        }

        private static ProductDto MapToDto(
            Product product,
            IEnumerable<ProductImage> images,
            IEnumerable<ProductParametr> parametrs,
            IEnumerable<ProductDescription> descriptions,
            IEnumerable<ProductDescriptionLanguage> descriptionLanguages,
            IReadOnlyList<int> promotionIds)
        {
            var productImageList = images.Where(x => x.Type).Select(x => x.ImageUrl).ToList();
            var productDatasheetList = images.Where(x => !x.Type).Select(x => x.ImageUrl).ToList();
            var parametrList = parametrs
                .OrderByDescending(x => product.InStock && x.Count.GetValueOrDefault() > 0 && x.Amount.GetValueOrDefault() > 0)
                .ThenByDescending(x => x.Count.GetValueOrDefault() > 0)
                .ThenByDescending(x => x.Amount.GetValueOrDefault() > 0)
                .Select(x => new ProductParametrDto(x.TechnicalPower, x.Effectiveness, x.Count, x.Amount))
                .ToList();
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
                productImageList,
                productDatasheetList,
                parametrList,
                descriptionList,
                promotionIds);
        }

        public async Task<ApiResponse<NoContentDto>> ShowHomePage(ProductShowHomePageDto dto, CancellationToken ct = default)
        {
            
            var productRepo = _uow.Repository<Product>();
            var product = await productRepo.FirstOrDefaultAsync(x => x.Id == dto.ProductId && x.IsActive, ct);

            product.InHomePage = dto.Show;
            productRepo.Update(product);
            await _uow.SaveChangesAsync(ct);
            _productSearchService.Invalidate();

            return  ApiResponse<NoContentDto>.SuccessResponse(null);
        }

        public async Task<ApiResponse<int>> ShowHomePageProductCount(CancellationToken ct = default)
        {
            var count = await _uow.Repository<Product>().CountAsync(x => x.IsActive && x.InHomePage, ct);

            return ApiResponse<int>.SuccessResponse(count);
        }
    }
}
