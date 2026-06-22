using Volt.Application.Dtos;
using Volt.Application.Dtos.PartnershipType;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class PartnershipTypeService : IPartnershipTypeService
    {
        private static readonly LanguageCode[] RequiredLanguages =
        [
            LanguageCode.AZ,
            LanguageCode.EN,
            LanguageCode.RU,
            LanguageCode.TR
        ];

        private readonly IUnitOfWork _uow;

        public PartnershipTypeService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<IReadOnlyList<PartnershipTypeDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var partnershipTypes = await _uow.Repository<PartnershipType>().ListNoTrackingAsync(x => x.IsActive, ct);
            var languages = await _uow.Repository<PartnershipTypeLanguage>().ListNoTrackingAsync(ct);

            var result = partnershipTypes
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => MapToDto(
                    x,
                    languages
                        .Where(l => l.PartnershipTypeId == x.Id)
                        .OrderBy(l => l.Id)
                        .ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<PartnershipTypeDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<PartnershipTypeDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var partnershipType = await _uow.Repository<PartnershipType>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (partnershipType is null)
            {
                return ApiResponse<PartnershipTypeDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND);
            }

            var dto = await BuildDtoAsync(id, null, ct);
            return ApiResponse<PartnershipTypeDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<PartnershipTypeDto>> CreateAsync(PartnershipTypeCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request?.Languages);
            if (languageValidation is not null)
            {
                return ApiResponse<PartnershipTypeDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                var partnershipType = new PartnershipType
                {
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await _uow.Repository<PartnershipType>().AddAsync(partnershipType, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var item in request.Languages)
                {
                    await _uow.Repository<PartnershipTypeLanguage>().AddAsync(new PartnershipTypeLanguage
                    {
                        PartnershipTypeId = partnershipType.Id,
                        LanguageCode = item.LanguageCode,
                        Name = item.Name.Trim(),
                        IsActive = true
                    }, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(partnershipType.Id, languageCode, ct);
                return ApiResponse<PartnershipTypeDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<PartnershipTypeDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the partnership type.");
            }
        }

        public async Task<ApiResponse<PartnershipTypeDto>> UpdateAsync(int id, PartnershipTypeUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var repo = _uow.Repository<PartnershipType>();
            var partnershipType = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (partnershipType is null)
            {
                return ApiResponse<PartnershipTypeDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request?.Languages);
            if (languageValidation is not null)
            {
                return ApiResponse<PartnershipTypeDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                partnershipType.IsActive = request.IsActive;
                partnershipType.UpdatedAt = DateTime.UtcNow;

                repo.Update(partnershipType);

                await UpsertLanguagesAsync(id, request.Languages, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(id, languageCode, ct);
                return ApiResponse<PartnershipTypeDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<PartnershipTypeDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the partnership type.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<PartnershipType>();
            var partnershipType = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (partnershipType is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND,
                    ErrorCode.PARTNERSHIP_TYPE_NOT_FOUND);
            }

            try
            {
                partnershipType.IsActive = false;
                partnershipType.UpdatedAt = DateTime.UtcNow;
                repo.Update(partnershipType);

                var langRepo = _uow.Repository<PartnershipTypeLanguage>();
                var languages = await langRepo.ListNoTrackingAsync(x => x.PartnershipTypeId == id, ct);
                foreach (var lang in languages)
                {
                    var tracked = await langRepo.FirstOrDefaultAsync(x => x.Id == lang.Id, ct);
                    if (tracked is not null)
                    {
                        tracked.IsActive = false;
                        langRepo.Update(tracked);
                    }
                }

                await _uow.SaveChangesAsync(ct);
                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the partnership type.");
            }
        }

        private string? ValidateLanguages<TLanguage>(IEnumerable<TLanguage>? languages)
            where TLanguage : class
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_PARTNERSHIP_TYPE_REQUEST;
            }

            var codes = languages
                .Select(x =>
                {
                    return x switch
                    {
                        PartnershipTypeLanguageCreateRequest c => c.LanguageCode,
                        PartnershipTypeLanguageUpdateRequest u => u.LanguageCode,
                        _ => default(LanguageCode)
                    };
                })
                .ToList();

            if (codes.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.PARTNERSHIP_TYPE_LANGUAGE_DUPLICATE;
            }

            if (codes.Count != RequiredLanguages.Length
                || RequiredLanguages.Any(required => !codes.Contains(required)))
            {
                return ErrorCode.PARTNERSHIP_TYPE_ALL_LANGUAGES_REQUIRED;
            }

            return null;
        }

        private async Task UpsertLanguagesAsync(int partnershipTypeId, List<PartnershipTypeLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var repo = _uow.Repository<PartnershipTypeLanguage>();
            var existing = await repo.ListNoTrackingAsync(x => x.PartnershipTypeId == partnershipTypeId, ct);

            foreach (var item in languages)
            {
                var existingLang = existing.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);
                if (existingLang is null)
                {
                    await repo.AddAsync(new PartnershipTypeLanguage
                    {
                        PartnershipTypeId = partnershipTypeId,
                        LanguageCode = item.LanguageCode,
                        Name = item.Name.Trim(),
                        IsActive = item.IsActive
                    }, ct);
                }
                else
                {
                    var tracked = await repo.FirstOrDefaultAsync(x => x.Id == existingLang.Id, ct);
                    if (tracked is not null)
                    {
                        tracked.Name = item.Name.Trim();
                        tracked.IsActive = item.IsActive;
                        repo.Update(tracked);
                    }
                }
            }
        }

        private async Task<PartnershipTypeDto> BuildDtoAsync(int partnershipTypeId, LanguageCode? languageCode, CancellationToken ct)
        {
            var partnershipType = await _uow.Repository<PartnershipType>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == partnershipTypeId, ct);
            var languages = await _uow.Repository<PartnershipTypeLanguage>()
                .ListNoTrackingAsync(x => x.PartnershipTypeId == partnershipTypeId, ct);

            return MapToDto(partnershipType, languages.OrderBy(x => x.Id).ToList(), languageCode);
        }

        private static PartnershipTypeDto MapToDto(PartnershipType partnershipType, IReadOnlyList<PartnershipTypeLanguage> languages, LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode).ToList();

            return new(
                partnershipType.Id,
                partnershipType.IsActive,
                partnershipType.CreatedAt,
                partnershipType.UpdatedAt,
                filteredLanguages.Select(x => new PartnershipTypeLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Name,
                    x.IsActive)).ToList());
        }
    }
}
