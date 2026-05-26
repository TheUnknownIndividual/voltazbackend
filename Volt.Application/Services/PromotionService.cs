using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Dtos.ProdcutCategory;
using Volt.Application.Dtos.Promotion;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public class PromotionService : IPromotionService
    {
        private readonly IUnitOfWork _uow;

        public PromotionService(IUnitOfWork uow)
        {
            _uow = uow;
        }
        public async Task<ApiResponse<PromotionDto>> CreateAsync(PromotionCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<PromotionDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                var promotionRepo = _uow.Repository<Promotion>();

                var promotion = new Promotion
                {
                    IsActive = true,
                };

                await promotionRepo.AddAsync(promotion, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var item in request.Languages)
                {
                    var language = new PromotionLanguage
                    {
                        PromotionId = promotion.Id,
                        LanguageCode = item.LanguageCode,
                        PromotionName = item.PromotionName,
                        IsActive = true
                    };

                    await _uow.Repository<PromotionLanguage>().AddAsync(language, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(promotion.Id, null, ct);
                return ApiResponse<PromotionDto>.SuccessResponse(dto);
            }
            catch (Exception ex)
            {

                return ApiResponse<PromotionDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    ex.Message);
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var promotionRepo = _uow.Repository<Promotion>();
            var promotion = await promotionRepo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (promotion is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.PROMOTION_NOT_FOUND,
                    ErrorCode.PROMOTION_NOT_FOUND);
            }

            try
            {
                promotion.IsActive = false;
                promotionRepo.Update(promotion);

                var languageRepo = _uow.Repository<PromotionLanguage>();

                var languages = await languageRepo.ListNoTrackingAsync(x => x.PromotionId == id, ct);
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

        public async Task<ApiResponse<IReadOnlyList<PromotionDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var promotions = await _uow.Repository<Promotion>().ListNoTrackingAsync(x => x.IsActive, ct);
            var languages = await _uow.Repository<PromotionLanguage>().ListNoTrackingAsync(ct);

            var results = promotions
                .OrderBy(x => x.Id)
                .Select(x => MapToDto(
                    x,
                    languages.Where(l => l.PromotionId == x.Id).OrderBy(l => l.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<PromotionDto>>.SuccessResponse(results);
        }

        public async Task<ApiResponse<PromotionDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var promotion = await _uow.Repository<Promotion>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (promotion is null)
            {
                return ApiResponse<PromotionDto>.ErrorResponse(
                    ErrorCode.PROMOTION_NOT_FOUND,
                    ErrorCode.PROMOTION_NOT_FOUND);
            }

            var dto = await BuildDtoAsync(id, null, ct);
            return ApiResponse<PromotionDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<PromotionDto>> UpdateAsync(int id, PromotionUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var promotionRepo = _uow.Repository<Promotion>();

            var promotion = await promotionRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (promotion is null)
            {
                return ApiResponse<PromotionDto>.ErrorResponse(
                    ErrorCode.PROMOTION_NOT_FOUND,
                    ErrorCode.PROMOTION_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<PromotionDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                await UpsertLanguagesAsync(id, request.Languages , ct);

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(id, languageCode, ct);
                return ApiResponse<PromotionDto>.SuccessResponse(dto);

            }
            catch (Exception)
            {

                return ApiResponse<PromotionDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the about record.");
            }
        }

        private string? ValidateLanguages(List<LanguageCode> languages)
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_PROMOTION_REQUEST;
            }

            if (languages.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.PROMOTION_LANGUAGE_DUPLICATE;
            }

            return null;
        }
        private async Task UpsertLanguagesAsync(int promotionId, List<PromotionLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var languageRepo = _uow.Repository<PromotionLanguage>();
            var existingLanguages = await languageRepo.ListNoTrackingAsync(x => x.PromotionId == promotionId, ct);

            foreach (var item in languages)
            {
                var existingLanguage = existingLanguages.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);

                if (existingLanguage is null)
                {
                    var newLanguage = new PromotionLanguage
                    {
                        PromotionId = promotionId,
                        LanguageCode = item.LanguageCode,
                        PromotionName = item.PromotionName,
                    };

                    await languageRepo.AddAsync(newLanguage, ct);
                }
                else
                {
                    var trackedLanguage = await languageRepo.FirstOrDefaultAsync(x => x.Id == existingLanguage.Id, ct);
                    if (trackedLanguage is not null)
                    {
                        trackedLanguage.PromotionName = item.PromotionName;
                        languageRepo.Update(trackedLanguage);
                    }
                }
            }
        }

        private async Task<PromotionDto> BuildDtoAsync(int promotionId, LanguageCode? languageCode, CancellationToken ct)
        {
            var promotion = await _uow.Repository<Promotion>().FirstOrDefaultNoTrackingAsync(x => x.Id == promotionId, ct);
            var languages = await _uow.Repository<PromotionLanguage>().ListNoTrackingAsync(x => x.PromotionId
            == promotionId, ct);

            return MapToDto(
                promotion,
                languages.OrderBy(x => x.Id).ToList(),
                languageCode
                );
        }

        private PromotionDto MapToDto(
            Promotion promotion,
            IEnumerable<PromotionLanguage> languages,
            LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode);

            return new PromotionDto(
                promotion.Id,
                filteredLanguages.Select(x => new PromotionLanguageDto(
                    x.LanguageCode,
                    x.PromotionName
                    )).ToList()
                    .ToList());

        }

        
    }
}
