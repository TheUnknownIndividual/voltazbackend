using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Dtos.ServiceManagement;
using Volt.Application.Dtos.Step;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public class ServiceManagementService : IServiceManagementService
    {
        private readonly IUnitOfWork _uow;

        public ServiceManagementService(IUnitOfWork uow)
        {
            _uow = uow;
        }
        public async Task<ApiResponse<ServiceManagementDto>> CreateAsync(ServiceManagementCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var validationError = ValidateRequest(request.Languages);
            if (validationError is not null || !Enum.IsDefined(request.Category))
            {
                var error = validationError ?? ErrorCode.INVALID_SERVICE_REQUEST;
                return ApiResponse<ServiceManagementDto>.ErrorResponse(error, error);
            }

            var normalizedSlug = NormalizeSlug(request.DetailPageSlug);
            if (normalizedSlug is not null && await _uow.Repository<ServiceManagement>().AnyAsync(x => x.DetailPageSlug == normalizedSlug, ct))
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(ErrorCode.INVALID_SERVICE_REQUEST, "Service page URL is already in use");
            }

            try
            {
               
                var service = new ServiceManagement
                {
                    IsActive = true,
                    Icon = request.Icon,
                    Category = request.Category,
                    ReadMoreUrl = NormalizeOptional(request.ReadMoreUrl),
                    DetailPageSlug = normalizedSlug,
                    BannerImageUrl = NormalizeOptional(request.BannerImageUrl),
                    CreatedAt = DateTime.UtcNow
                };

                await _uow.Repository<ServiceManagement>().AddAsync(service, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var language in request.Languages)
                {
                    await _uow.Repository<ServiceManagementLanguage>().AddAsync(new ServiceManagementLanguage
                    {
                        ServiceMagamentId = service.Id,
                        LanguageCode = language.LanguageCode,
                        Title = language.Title.Trim(),
                        Description = language.Description.Trim(),
                        Content1 = NormalizeOptional(language.Content1),
                        Content2 = NormalizeOptional(language.Content2),
                        Content3 = NormalizeOptional(language.Content3),
                        Content4 = NormalizeOptional(language.Content4),
                        DetailContentHtml = NormalizeOptional(language.DetailContentHtml),
                        SeoTitle = NormalizeOptional(language.SeoTitle),
                        SeoDescription = NormalizeOptional(language.SeoDescription),
                        SeoKeywords = NormalizeOptional(language.SeoKeywords),
                        IsActive = true,
                    }, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildServiceDtoAsync(service.Id, languageCode, ct);
                return ApiResponse<ServiceManagementDto>.SuccessResponse(dto);
            }
            catch 
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occured while creating the service");
            }
        }

        public async Task<ApiResponse<IReadOnlyList<ServiceManagementDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var services = await _uow.Repository<ServiceManagement>().ListNoTrackingAsync(x => x.IsActive, ct);
            var languages = await _uow.Repository<ServiceManagementLanguage>().ListNoTrackingAsync(ct);

            var result = services
                .OrderBy(x => x.Id)
                .Select(x => MapToServiceManagementDto(
                    x,
                    languages.Where(l => l.ServiceMagamentId == x.Id).OrderBy(l => l.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<ServiceManagementDto>>.SuccessResponse(result);

        }

        public async Task<ApiResponse<ServiceManagementDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var service = await _uow.Repository<ServiceManagement>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (service is null)
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(ErrorCode.SERVICE_NOT_FOUND, "Service not found");
            }

            var dto = await BuildServiceDtoAsync(service.Id, null, ct);
            return ApiResponse<ServiceManagementDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<ServiceManagementDto>> GetBySlugAsync(string slug, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var normalizedSlug = NormalizeSlug(slug);
            var service = await _uow.Repository<ServiceManagement>()
                .FirstOrDefaultNoTrackingAsync(x => x.IsActive && x.DetailPageSlug == normalizedSlug, ct);

            if (service is null)
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(ErrorCode.SERVICE_NOT_FOUND, "Service page not found");
            }

            var dto = await BuildServiceDtoAsync(service.Id, languageCode, ct);
            return ApiResponse<ServiceManagementDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<IReadOnlyList<ServiceCategorySettingDto>>> GetCategorySettingsAsync(CancellationToken ct = default)
        {
            var settings = await _uow.Repository<ServiceCategorySetting>().ListNoTrackingAsync(ct);
            var result = Enum.GetValues<ServiceCategory>()
                .Select(category => new ServiceCategorySettingDto(
                    category,
                    settings.FirstOrDefault(x => x.Category == category)?.IsReadMoreEnabled
                        ?? category == ServiceCategory.Corporate))
                .ToList();

            return ApiResponse<IReadOnlyList<ServiceCategorySettingDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<ServiceCategorySettingDto>> UpdateCategorySettingAsync(
            ServiceCategory category,
            ServiceCategorySettingUpdateRequest request,
            CancellationToken ct = default)
        {
            if (!Enum.IsDefined(category))
            {
                return ApiResponse<ServiceCategorySettingDto>.ErrorResponse(ErrorCode.INVALID_SERVICE_REQUEST, "Invalid service category");
            }

            var repository = _uow.Repository<ServiceCategorySetting>();
            var setting = await repository.FirstOrDefaultAsync(x => x.Category == category, ct);
            if (setting is null)
            {
                setting = new ServiceCategorySetting
                {
                    Category = category,
                    IsReadMoreEnabled = request.IsReadMoreEnabled,
                    UpdatedAt = DateTime.UtcNow
                };
                await repository.AddAsync(setting, ct);
            }
            else
            {
                setting.IsReadMoreEnabled = request.IsReadMoreEnabled;
                setting.UpdatedAt = DateTime.UtcNow;
                repository.Update(setting);
            }

            await _uow.SaveChangesAsync(ct);
            return ApiResponse<ServiceCategorySettingDto>.SuccessResponse(new ServiceCategorySettingDto(category, setting.IsReadMoreEnabled));
        }

        public async Task<ApiResponse<ServiceManagementDto>> UpdateAsync(int id, ServiceManagementUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var serviceRepo = _uow.Repository<ServiceManagement>();
            var service = await serviceRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (service is null)
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(ErrorCode.SERVICE_NOT_FOUND, "Service not found");
            }

            var validationError = ValidateRequest(request.Languages);

            if (validationError is not null || !Enum.IsDefined(request.Category))
            {
                var error = validationError ?? ErrorCode.INVALID_SERVICE_REQUEST;
                return ApiResponse<ServiceManagementDto>.ErrorResponse(error, error);
            }

            var normalizedSlug = NormalizeSlug(request.DetailPageSlug);
            if (normalizedSlug is not null && await serviceRepo.AnyAsync(x => x.Id != id && x.DetailPageSlug == normalizedSlug, ct))
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(ErrorCode.INVALID_SERVICE_REQUEST, "Service page URL is already in use");
            }

            try
            {
                
                service.UpdatedAt = DateTime.UtcNow;
                service.Icon = request.Icon;
                service.Category = request.Category;
                service.ReadMoreUrl = NormalizeOptional(request.ReadMoreUrl);
                service.DetailPageSlug = normalizedSlug;
                service.BannerImageUrl = NormalizeOptional(request.BannerImageUrl);

                serviceRepo.Update(service);

                await UpsertLanguagesAsync(id, request.Languages, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildServiceDtoAsync(service.Id, languageCode, ct);
                return ApiResponse<ServiceManagementDto>.SuccessResponse(dto);
            }
            catch 
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occured while updating the service");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var serviceRepo = _uow.Repository<ServiceManagement>();
            var service = await serviceRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (service is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.SERVICE_NOT_FOUND, "Service not found");
            }

            try
            {
                service.IsActive = false;
                service.UpdatedAt = DateTime.UtcNow;
                serviceRepo.Update(service);

                var languageRepo = _uow.Repository<ServiceManagementLanguage>();
                var languages = await languageRepo.ListNoTrackingAsync(x => x.ServiceMagamentId == id, ct);

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
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.SERVER_ERROR, "An error occured while deleting the service");
            }
        }

        private string ValidateRequest<TLanguage>(IEnumerable<TLanguage> languages)
            where TLanguage : class
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_SERVICE_REQUEST;
            }

            if (languages.Any(x => x switch
                {
                    ServiceManagementLanguageCreateRequest c => !Enum.IsDefined(c.LanguageCode) || string.IsNullOrWhiteSpace(c.Title) || string.IsNullOrWhiteSpace(c.Description),
                    ServiceManagementLanguageUpdateRequest u => !Enum.IsDefined(u.LanguageCode) || string.IsNullOrWhiteSpace(u.Title) || string.IsNullOrWhiteSpace(u.Description),
                    _ => true
                }))
            {
                return ErrorCode.INVALID_SERVICE_REQUEST;
            }

           
            var languageCodes = languages
                .Select(x =>
                {
                    return x switch
                    {
                        ServiceManagementLanguageCreateRequest c => c.LanguageCode,
                        ServiceManagementLanguageUpdateRequest u => u.LanguageCode,
                        _ => default
                    };
                })
                .ToList();

            if (languageCodes.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.SERVICE_LANGUAGE_DUPLICATE;
            }

            return null;
        }
        private async Task UpsertLanguagesAsync(int stepId, List<ServiceManagementLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var languageRepo = _uow.Repository<ServiceManagementLanguage>();
            var existingLanguages = await languageRepo.ListNoTrackingAsync(x => x.ServiceMagamentId == stepId, ct);

            foreach (var item in languages)
            {
                var existingLanguage = existingLanguages.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);

                if (existingLanguage is null)
                {
                    await languageRepo.AddAsync(new ServiceManagementLanguage
                    {
                        ServiceMagamentId = stepId,
                        LanguageCode = item.LanguageCode,
                        Title = item.Title.Trim(),
                        Description = item.Description.Trim(),
                        Content1 = NormalizeOptional(item.Content1),
                        Content2 = NormalizeOptional(item.Content2),
                        Content3 = NormalizeOptional(item.Content3),
                        Content4 = NormalizeOptional(item.Content4),
                        DetailContentHtml = NormalizeOptional(item.DetailContentHtml),
                        SeoTitle = NormalizeOptional(item.SeoTitle),
                        SeoDescription = NormalizeOptional(item.SeoDescription),
                        SeoKeywords = NormalizeOptional(item.SeoKeywords),
                        IsActive = item.IsActive,
                    }, ct);
                }
                else
                {
                    var trackedLanguage = await languageRepo.FirstOrDefaultAsync(x => x.Id == existingLanguage.Id, ct);
                    if (trackedLanguage is not null)
                    {
                        trackedLanguage.Title = item.Title.Trim();
                        trackedLanguage.Description = item.Description.Trim();
                        trackedLanguage.Content1 = NormalizeOptional(item.Content1);
                        trackedLanguage.Content2 = NormalizeOptional(item.Content2);
                        trackedLanguage.Content3 = NormalizeOptional(item.Content3);
                        trackedLanguage.Content4 = NormalizeOptional(item.Content4);
                        trackedLanguage.DetailContentHtml = NormalizeOptional(item.DetailContentHtml);
                        trackedLanguage.SeoTitle = NormalizeOptional(item.SeoTitle);
                        trackedLanguage.SeoDescription = NormalizeOptional(item.SeoDescription);
                        trackedLanguage.SeoKeywords = NormalizeOptional(item.SeoKeywords);
                        trackedLanguage.IsActive = item.IsActive;
                        languageRepo.Update(trackedLanguage);
                    }
                }
            }
        }
        private async Task<ServiceManagementDto> BuildServiceDtoAsync(int servisId, LanguageCode? languageCode, CancellationToken ct)
        {
            var service = await _uow.Repository<ServiceManagement>().FirstOrDefaultNoTrackingAsync(x => x.Id == servisId, ct);
            var languages = await _uow.Repository<ServiceManagementLanguage>().ListNoTrackingAsync(x => x.ServiceMagamentId == servisId, ct);

            return MapToServiceManagementDto(service, languages.OrderBy(x => x.Id).ToList(), languageCode);
        }
        private ServiceManagementDto MapToServiceManagementDto(ServiceManagement service, IEnumerable<ServiceManagementLanguage> languages, LanguageCode? languageCode)
        {
            var languageList = languages.ToList();
            var filteredLanguages = languageCode is null
                ? languageList
                : languageList.Where(x => x.LanguageCode == languageCode).ToList();

            if (languageCode is not null && !filteredLanguages.Any())
            {
                filteredLanguages = languageList.Where(x => x.LanguageCode == LanguageCode.AZ).ToList();
            }

            return new ServiceManagementDto(
                service.Id,
                service.IsActive,
                service.Icon,
                service.Category,
                service.ReadMoreUrl,
                service.DetailPageSlug,
                service.BannerImageUrl,
                service.CreatedAt,
                service.UpdatedAt,
                filteredLanguages.Select(x => new ServiceManagementLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.Content1,
                    x.Content2,
                    x.Content3,
                    x.Content4,
                    x.DetailContentHtml,
                    x.SeoTitle,
                    x.SeoDescription,
                    x.SeoKeywords,
                    x.IsActive)).ToList());
        }

        private static string NormalizeOptional(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string NormalizeSlug(string value)
            => string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim().Trim('/').ToLowerInvariant();
    }
}
