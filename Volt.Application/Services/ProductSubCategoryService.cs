using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Dtos.ProdcutCategory;
using Volt.Application.Dtos.ProductSubCategory;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public class ProductSubCategoryService : IProductSubCategoryService
    {
        private readonly IUnitOfWork _uow;

        public ProductSubCategoryService(IUnitOfWork uow)
        {
            _uow = uow;  
        }
        public async Task<ApiResponse<ProductSubCategoryDto>> CreateAsync(ProductSubCategoryCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<ProductSubCategoryDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                var ProductSubCategoryRepo = _uow.Repository<ProductSubCategory>();

                var productSubCategory = new ProductSubCategory
                {
                    ProductCategoryId = request.ProductCategoryId,
                    IsActive = true,
                };

                await ProductSubCategoryRepo.AddAsync(productSubCategory, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var item in request.Languages)
                {
                    var language = new ProductSubCategoryLanguage
                    {
                        ProductSubCategoryId = productSubCategory.Id,
                        LanguageCode = item.LanguageCode,
                        SubCategoryName = item.SubCategoryName,
                        IsActive = true
                    };

                    await _uow.Repository<ProductSubCategoryLanguage>().AddAsync(language, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(productSubCategory.Id, null, ct);
                return ApiResponse<ProductSubCategoryDto>.SuccessResponse(dto);
            }
            catch (Exception ex)
            {

                return ApiResponse<ProductSubCategoryDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    ex.Message);
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var productSubCategoryRepo = _uow.Repository<ProductSubCategory>();
            var productSubCategory = await productSubCategoryRepo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (productSubCategory is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.PRODUCT_SUBCATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_SUBCATEGORY_NOT_FOUND);
            }

            try
            {
                productSubCategory.IsActive = false;
                productSubCategoryRepo.Update(productSubCategory);

                var languageRepo = _uow.Repository<ProductSubCategoryLanguage>();

                var languages = await languageRepo.ListNoTrackingAsync(x => x.ProductSubCategoryId == id, ct);
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

        public async Task<ApiResponse<IReadOnlyList<ProductSubCategoryDto>>> GetAllAsync(int ProductCategoryId, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var productSubCategories = await _uow.Repository<ProductSubCategory>().ListNoTrackingAsync(
                x =>x.ProductCategoryId == ProductCategoryId &&
                x.IsActive, ct);

            var languages = await _uow.Repository<ProductSubCategoryLanguage>().ListNoTrackingAsync(ct);

            var results = productSubCategories
                .OrderBy(x => x.Id)
                .Select(x => MapToDto(
                    x,
                    languages.Where(l => l.ProductSubCategoryId == x.Id).OrderBy(l => l.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<ProductSubCategoryDto>>.SuccessResponse(results);
        }

        public async Task<ApiResponse<ProductSubCategoryDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var productSubCategory = await _uow.Repository<ProductSubCategory>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (productSubCategory is null)
            {
                return ApiResponse<ProductSubCategoryDto>.ErrorResponse(
                    ErrorCode.PRODUCT_SUBCATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_SUBCATEGORY_NOT_FOUND);
            }

            var dto = await BuildDtoAsync(id, null, ct);
            return ApiResponse<ProductSubCategoryDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<ProductSubCategoryDto>> UpdateAsync(int id, ProductSubCategoryUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var productSubCategoryRepo = _uow.Repository<ProductSubCategory>();

            var productSubCategory = await productSubCategoryRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (productSubCategory is null)
            {
                return ApiResponse<ProductSubCategoryDto>.ErrorResponse(
                    ErrorCode.PRODUCT_SUBCATEGORY_NOT_FOUND,
                    ErrorCode.PRODUCT_SUBCATEGORY_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<ProductSubCategoryDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                await UpsertLanguagesAsync(id, request.Languages, ct);

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(id, languageCode, ct);
                return ApiResponse<ProductSubCategoryDto>.SuccessResponse(dto);

            }
            catch (Exception)
            {

                return ApiResponse<ProductSubCategoryDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the about record.");
            }
        }

        private string? ValidateLanguages(List<LanguageCode> languages)
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_PRODUCT_SUBCATEGORY_REQUEST;
            }

            if (languages.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.PRODUCT_SUBCATEGORY_LANGUAGE_DUPLICATE;
            }

            return null;
        }
        private async Task UpsertLanguagesAsync(int productSubCategoryId, List<ProductSubCategoryLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var languageRepo = _uow.Repository<ProductSubCategoryLanguage>();
            var existingLanguages = await languageRepo.ListNoTrackingAsync(x => x.ProductSubCategoryId == productSubCategoryId, ct);

            foreach (var item in languages)
            {
                var existingLanguage = existingLanguages.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);

                if (existingLanguage is null)
                {
                    var newLanguage = new ProductSubCategoryLanguage
                    {
                        ProductSubCategoryId = productSubCategoryId,
                        LanguageCode = item.LanguageCode,
                        SubCategoryName = item.SubCategoryName,
                    };

                    await languageRepo.AddAsync(newLanguage, ct);
                }
                else
                {
                    var trackedLanguage = await languageRepo.FirstOrDefaultAsync(x => x.Id == existingLanguage.Id, ct);
                    if (trackedLanguage is not null)
                    {
                        trackedLanguage.SubCategoryName = item.SubCategoryName;
                        languageRepo.Update(trackedLanguage);
                    }
                }
            }
        }

        private async Task<ProductSubCategoryDto> BuildDtoAsync(int productSubCategoryid, LanguageCode? languageCode, CancellationToken ct)
        {
            var productSubCategory = await _uow.Repository<ProductSubCategory>().FirstOrDefaultNoTrackingAsync(x => x.Id == productSubCategoryid, ct);
            var languages = await _uow.Repository<ProductSubCategoryLanguage>().ListNoTrackingAsync(x => x.ProductSubCategoryId
            == productSubCategoryid, ct);

            return MapToDto(
                productSubCategory,
                languages.OrderBy(x => x.Id).ToList(),
                languageCode
                );
        }

        private ProductSubCategoryDto MapToDto(
            ProductSubCategory productCategory,
            IEnumerable<ProductSubCategoryLanguage> languages,
            LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode);

            return new ProductSubCategoryDto(
                productCategory.Id,
                filteredLanguages.Select(x => new ProductSubCategoryLanguageDto(
                    x.LanguageCode,
                    x.SubCategoryName
                    )).ToList()
                    .ToList());

        }
    }
}
