using Volt.Application.Dtos;
using Volt.Application.Dtos.ProductBrand;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ProductBrandService : IProductBrandService
    {
        private readonly IUnitOfWork _uow;

        public ProductBrandService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<ProductBrandDto>> CreateAsync(ProductBrandCreateRequest request, CancellationToken ct = default)
        {
            var categoryError = await ValidateProductCategoryAsync(request.ProductCategoryId, ct);
            if (categoryError is not null)
            {
                return ApiResponse<ProductBrandDto>.ErrorResponse(categoryError, categoryError);
            }

            try
            {
                var brand = new ProductBrand
                {
                    ProductCategoryId = request.ProductCategoryId,
                    Name = request.Name.Trim(),
                    IsActive = true,
                };

                await _uow.Repository<ProductBrand>().AddAsync(brand, ct);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ProductBrandDto>.SuccessResponse(MapToDto(brand));
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductBrandDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "Server Side Error");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var brandRepo = _uow.Repository<ProductBrand>();
            var brand = await brandRepo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (brand is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.PRODUCT_BRAND_NOT_FOUND,
                    ErrorCode.PRODUCT_BRAND_NOT_FOUND);
            }

            try
            {
                brand.IsActive = false;
                brandRepo.Update(brand);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the product brand.");
            }
        }

        public async Task<ApiResponse<IReadOnlyList<ProductBrandDto>>> GetAllAsync(int productCategoryId, CancellationToken ct = default)
        {
            var categoryError = await ValidateProductCategoryAsync(productCategoryId, ct);
            if (categoryError is not null)
            {
                return ApiResponse<IReadOnlyList<ProductBrandDto>>.ErrorResponse(categoryError, categoryError);
            }

            var brands = await _uow.Repository<ProductBrand>().ListNoTrackingAsync(
                x => x.ProductCategoryId == productCategoryId && x.IsActive,
                ct);

            var results = brands
                .OrderBy(x => x.Id)
                .Select(MapToDto)
                .ToList();

            return ApiResponse<IReadOnlyList<ProductBrandDto>>.SuccessResponse(results);
        }

        public async Task<ApiResponse<ProductBrandDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var brand = await _uow.Repository<ProductBrand>().FirstOrDefaultNoTrackingAsync(
                x => x.Id == id && x.IsActive,
                ct);

            if (brand is null)
            {
                return ApiResponse<ProductBrandDto>.ErrorResponse(
                    ErrorCode.PRODUCT_BRAND_NOT_FOUND,
                    ErrorCode.PRODUCT_BRAND_NOT_FOUND);
            }

            return ApiResponse<ProductBrandDto>.SuccessResponse(MapToDto(brand));
        }

        public async Task<ApiResponse<ProductBrandDto>> UpdateAsync(int id, ProductBrandUpdateRequest request, CancellationToken ct = default)
        {
            var brandRepo = _uow.Repository<ProductBrand>();
            var brand = await brandRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (brand is null)
            {
                return ApiResponse<ProductBrandDto>.ErrorResponse(
                    ErrorCode.PRODUCT_BRAND_NOT_FOUND,
                    ErrorCode.PRODUCT_BRAND_NOT_FOUND);
            }

            var categoryError = await ValidateProductCategoryAsync(request.ProductCategoryId, ct);
            if (categoryError is not null)
            {
                return ApiResponse<ProductBrandDto>.ErrorResponse(categoryError, categoryError);
            }

            try
            {
                brand.ProductCategoryId = request.ProductCategoryId;
                brand.Name = request.Name.Trim();
                brandRepo.Update(brand);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ProductBrandDto>.SuccessResponse(MapToDto(brand));
            }
            catch
            {
                return ApiResponse<ProductBrandDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the product brand.");
            }
        }

        private async Task<string?> ValidateProductCategoryAsync(int productCategoryId, CancellationToken ct)
        {
            var exists = await _uow.Repository<ProductCategory>().AnyAsync(
                x => x.Id == productCategoryId && x.IsActive,
                ct);

            return exists ? null : ErrorCode.PRODUCT_CATEGORY_NOT_FOUND;
        }

        private static ProductBrandDto MapToDto(ProductBrand brand) =>
            new(brand.Id, brand.ProductCategoryId, brand.Name);
    }
}
