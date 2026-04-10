using Volt.Application.Dtos;
using Volt.Application.Dtos.ApplicationType;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ApplicationTypeService : IApplicationTypeService
    {
        private readonly IUnitOfWork _uow;

        public ApplicationTypeService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<IReadOnlyList<ApplicationTypeDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var appTypes = await _uow.Repository<ApplicationType>().ListNoTrackingAsync(x => x.IsActive, ct);
            var languages = await _uow.Repository<ApplicationTypeLanguage>().ListNoTrackingAsync(ct);

            var result = appTypes
                // ServiceManagementId dolu olanlar əvvəl
                .OrderBy(x => x.ServiceManagementId is null)
                // qruplaşma: eyni ServiceManagementId yanaşı
                .ThenBy(x => x.ServiceManagementId ?? int.MaxValue)
                // qrup daxilində sabit sıralama
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => MapToDto(
                    x,
                    languages
                        .Where(l => l.ApplicationTypeId == x.Id)
                        .OrderBy(l => l.Id)
                        .ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<ApplicationTypeDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<ApplicationTypeDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var appType = await _uow.Repository<ApplicationType>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (appType is null)
            {
                return ApiResponse<ApplicationTypeDto>.ErrorResponse(
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND,
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND);
            }

            var dto = await BuildDtoAsync(id, languageCode, ct);
            return ApiResponse<ApplicationTypeDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<ApplicationTypeDto>> CreateAsync(ApplicationTypeCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request?.Languages);
            if (languageValidation is not null)
            {
                return ApiResponse<ApplicationTypeDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                if (request.ServiceManagementId is not null)
                {
                    var exists = await _uow.Repository<ServiceManagement>()
                        .AnyAsync(x => x.Id == request.ServiceManagementId && x.IsActive, ct);
                    if (!exists)
                    {
                        return ApiResponse<ApplicationTypeDto>.ErrorResponse(
                            ErrorCode.SERVICE_NOT_FOUND,
                            ErrorCode.SERVICE_NOT_FOUND);
                    }
                }

                var appType = new ApplicationType
                {
                    ServiceManagementId = request.ServiceManagementId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await _uow.Repository<ApplicationType>().AddAsync(appType, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var item in request.Languages)
                {
                    await _uow.Repository<ApplicationTypeLanguage>().AddAsync(new ApplicationTypeLanguage
                    {
                        ApplicationTypeId = appType.Id,
                        LanguageCode = item.LanguageCode,
                        Name = item.Name.Trim(),
                        IsActive = true
                    }, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(appType.Id, languageCode, ct);
                return ApiResponse<ApplicationTypeDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<ApplicationTypeDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the application type.");
            }
        }

        public async Task<ApiResponse<ApplicationTypeDto>> UpdateAsync(int id, ApplicationTypeUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ApplicationType>();
            var appType = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (appType is null)
            {
                return ApiResponse<ApplicationTypeDto>.ErrorResponse(
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND,
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request?.Languages);
            if (languageValidation is not null)
            {
                return ApiResponse<ApplicationTypeDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                if (request.ServiceManagementId is not null)
                {
                    var exists = await _uow.Repository<ServiceManagement>()
                        .AnyAsync(x => x.Id == request.ServiceManagementId && x.IsActive, ct);
                    if (!exists)
                    {
                        return ApiResponse<ApplicationTypeDto>.ErrorResponse(
                            ErrorCode.SERVICE_NOT_FOUND,
                            ErrorCode.SERVICE_NOT_FOUND);
                    }
                }

                appType.ServiceManagementId = request.ServiceManagementId;
                appType.IsActive = request.IsActive;
                appType.UpdatedAt = DateTime.UtcNow;

                repo.Update(appType);

                await UpsertLanguagesAsync(id, request.Languages, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(id, languageCode, ct);
                return ApiResponse<ApplicationTypeDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<ApplicationTypeDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the application type.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var repo = _uow.Repository<ApplicationType>();
            var appType = await repo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (appType is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND,
                    ErrorCode.APPLICATION_TYPE_NOT_FOUND);
            }

            try
            {
                appType.IsActive = false;
                appType.UpdatedAt = DateTime.UtcNow;
                repo.Update(appType);

                var langRepo = _uow.Repository<ApplicationTypeLanguage>();
                var languages = await langRepo.ListNoTrackingAsync(x => x.ApplicationTypeId == id, ct);
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
                    "An error occurred while deleting the application type.");
            }
        }

        private string? ValidateLanguages<TLanguage>(IEnumerable<TLanguage>? languages)
            where TLanguage : class
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_APPLICATION_TYPE_REQUEST;
            }

            var codes = languages
                .Select(x =>
                {
                    return x switch
                    {
                        ApplicationTypeLanguageCreateRequest c => c.LanguageCode,
                        ApplicationTypeLanguageUpdateRequest u => u.LanguageCode,
                        _ => default(LanguageCode)
                    };
                })
                .ToList();

            if (codes.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.APPLICATION_TYPE_LANGUAGE_DUPLICATE;
            }

            return null;
        }

        private async Task UpsertLanguagesAsync(int applicationTypeId, List<ApplicationTypeLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var repo = _uow.Repository<ApplicationTypeLanguage>();
            var existing = await repo.ListNoTrackingAsync(x => x.ApplicationTypeId == applicationTypeId, ct);

            foreach (var item in languages)
            {
                var existingLang = existing.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);
                if (existingLang is null)
                {
                    await repo.AddAsync(new ApplicationTypeLanguage
                    {
                        ApplicationTypeId = applicationTypeId,
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

        private async Task<ApplicationTypeDto> BuildDtoAsync(int applicationTypeId, LanguageCode? languageCode, CancellationToken ct)
        {
            var appType = await _uow.Repository<ApplicationType>()
                .FirstOrDefaultNoTrackingAsync(x => x.Id == applicationTypeId, ct);
            var languages = await _uow.Repository<ApplicationTypeLanguage>()
                .ListNoTrackingAsync(x => x.ApplicationTypeId == applicationTypeId, ct);

            return MapToDto(appType, languages.OrderBy(x => x.Id).ToList(), languageCode);
        }

        private static ApplicationTypeDto MapToDto(ApplicationType appType, IReadOnlyList<ApplicationTypeLanguage> languages, LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode).ToList();

            return new(
                appType.Id,
                appType.ServiceManagementId,
                appType.IsActive,
                appType.CreatedAt,
                appType.UpdatedAt,
                filteredLanguages.Select(x => new ApplicationTypeLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Name,
                    x.IsActive)).ToList());
        }
    }
}

