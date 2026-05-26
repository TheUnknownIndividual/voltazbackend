using Volt.Application.Dtos;
using Volt.Application.Dtos.ProductTechnology;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ProductTechnologyService : IProductTechnologyService
    {
        private readonly IUnitOfWork _uow;

        public ProductTechnologyService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<ProductTechnologyDto>> CreateAsync(ProductTechnologyCreateRequest request, CancellationToken ct = default)
        {
            var categoryError = await ValidateProductCategoryAsync(request.ProductCategoryId, ct);
            if (categoryError is not null)
            {
                return ApiResponse<ProductTechnologyDto>.ErrorResponse(categoryError, categoryError);
            }

            try
            {
                var technology = new ProductTechnology
                {
                    ProductCategoryId = request.ProductCategoryId,
                    Name = request.Name.Trim(),
                    IsActive = true,
                };

                await _uow.Repository<ProductTechnology>().AddAsync(technology, ct);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ProductTechnologyDto>.SuccessResponse(MapToDto(technology));
            }
            catch (Exception ex)
            {
                return ApiResponse<ProductTechnologyDto>.ErrorResponse(ErrorCode.SERVER_ERROR, ex.Message);
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var technologyRepo = _uow.Repository<ProductTechnology>();
            var technology = await technologyRepo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (technology is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.PRODUCT_TECHNOLOGY_NOT_FOUND,
                    ErrorCode.PRODUCT_TECHNOLOGY_NOT_FOUND);
            }

            try
            {
                technology.IsActive = false;
                technologyRepo.Update(technology);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the product technology.");
            }
        }

        public async Task<ApiResponse<IReadOnlyList<ProductTechnologyDto>>> GetAllAsync(int productCategoryId, CancellationToken ct = default)
        {
            var categoryError = await ValidateProductCategoryAsync(productCategoryId, ct);
            if (categoryError is not null)
            {
                return ApiResponse<IReadOnlyList<ProductTechnologyDto>>.ErrorResponse(categoryError, categoryError);
            }

            var technologies = await _uow.Repository<ProductTechnology>().ListNoTrackingAsync(
                x => x.ProductCategoryId == productCategoryId && x.IsActive,
                ct);

            var results = technologies
                .OrderBy(x => x.Id)
                .Select(MapToDto)
                .ToList();

            return ApiResponse<IReadOnlyList<ProductTechnologyDto>>.SuccessResponse(results);
        }

        public async Task<ApiResponse<ProductTechnologyDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var technology = await _uow.Repository<ProductTechnology>().FirstOrDefaultNoTrackingAsync(
                x => x.Id == id && x.IsActive,
                ct);

            if (technology is null)
            {
                return ApiResponse<ProductTechnologyDto>.ErrorResponse(
                    ErrorCode.PRODUCT_TECHNOLOGY_NOT_FOUND,
                    ErrorCode.PRODUCT_TECHNOLOGY_NOT_FOUND);
            }

            return ApiResponse<ProductTechnologyDto>.SuccessResponse(MapToDto(technology));
        }

        public async Task<ApiResponse<ProductTechnologyDto>> UpdateAsync(int id, ProductTechnologyUpdateRequest request, CancellationToken ct = default)
        {
            var technologyRepo = _uow.Repository<ProductTechnology>();
            var technology = await technologyRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (technology is null)
            {
                return ApiResponse<ProductTechnologyDto>.ErrorResponse(
                    ErrorCode.PRODUCT_TECHNOLOGY_NOT_FOUND,
                    ErrorCode.PRODUCT_TECHNOLOGY_NOT_FOUND);
            }

            var categoryError = await ValidateProductCategoryAsync(request.ProductCategoryId, ct);
            if (categoryError is not null)
            {
                return ApiResponse<ProductTechnologyDto>.ErrorResponse(categoryError, categoryError);
            }

            try
            {
                technology.ProductCategoryId = request.ProductCategoryId;
                technology.Name = request.Name.Trim();
                technologyRepo.Update(technology);
                await _uow.SaveChangesAsync(ct);

                return ApiResponse<ProductTechnologyDto>.SuccessResponse(MapToDto(technology));
            }
            catch
            {
                return ApiResponse<ProductTechnologyDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the product technology.");
            }
        }

        private async Task<string?> ValidateProductCategoryAsync(int productCategoryId, CancellationToken ct)
        {
            var exists = await _uow.Repository<ProductCategory>().AnyAsync(
                x => x.Id == productCategoryId && x.IsActive,
                ct);

            return exists ? null : ErrorCode.PRODUCT_CATEGORY_NOT_FOUND;
        }

        private static ProductTechnologyDto MapToDto(ProductTechnology technology) =>
            new(technology.Id, technology.ProductCategoryId, technology.Name);
    }
}
