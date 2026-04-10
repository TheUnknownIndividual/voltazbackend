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
            var validationError = ValidateRequest(request.Languages, request.ImagePath);
            if (validationError is not null)
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(validationError, validationError);
            }

            try
            {
                int nextPosition = (await _uow.Repository<ServiceManagement>().MaxAsync(x => x.Position, ct)) + 1;

                var service = new ServiceManagement
                {
                    ImagePath = request.ImagePath,
                    Position = nextPosition,
                    IsActive = true,
                    ActiveStatus = true,
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
                        IsActive = true,
                    }, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var appType = new ApplicationType
                {
                    ServiceManagementId = service.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await _uow.Repository<ApplicationType>().AddAsync(appType, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var language in request.Languages)
                {
                    await _uow.Repository<ApplicationTypeLanguage>().AddAsync(new ApplicationTypeLanguage
                    {
                        ApplicationTypeId = appType.Id,
                        LanguageCode = language.LanguageCode,
                        Name = language.Title.Trim(),
                        IsActive = true
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
            var services = await _uow.Repository<ServiceManagement>().ListNoTrackingAsync(x => x.IsActive && x.ActiveStatus, ct);
            var languages = await _uow.Repository<ServiceManagementLanguage>().ListNoTrackingAsync(ct);

            var result = services
                .OrderBy(x => x.Position)
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

            var dto = await BuildServiceDtoAsync(service.Id, languageCode, ct);
            return ApiResponse<ServiceManagementDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<ServiceManagementDto>> UpdateAsync(int id, ServiceManagementUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var serviceRepo = _uow.Repository<ServiceManagement>();
            var service = await serviceRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (service is null)
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(ErrorCode.SERVICE_NOT_FOUND, "Service not found");
            }

            var validationError = ValidateRequest(request.Languages, request.ImagePath);

            if (validationError is not null)
            {
                return ApiResponse<ServiceManagementDto>.ErrorResponse(validationError, validationError);
            }

            try
            {
                service.ImagePath = request.ImagePath.Trim();
                service.Position = request.Position;
                service.IsActive = request.IsActive;
                service.UpdatedAt = DateTime.UtcNow;

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

        public async Task<ApiResponse<NoContentDto>> ReorderAsync(List<ServiceManagementReorderRequest> request, CancellationToken ct = default)
        {
            if (request is null || !request.Any())
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_SERVICE_REORDER_REQUEST,
                    "Reorder request cannot be empty.");
            }

            if (request.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_SERVICE_REORDER_REQUEST,
                    "Duplicate service ids detected.");
            }

            if (request.GroupBy(x => x.Position).Any(g => g.Count() > 1))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_SERVICE_REORDER_REQUEST,
                    "Duplicate positions detected.");
            }

            var ids = request.Select(x => x.Id).ToList();
            var serviceRepo = _uow.Repository<ServiceManagement>();
            var services = await serviceRepo.ListNoTrackingAsync(x => ids.Contains(x.Id), ct);

            if (services.Count != ids.Count)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_SERVICE_REORDER_REQUEST,
                    "One or more service records were not found.");
            }

            try
            {
                foreach (var item in request)
                {
                    var trackedService = await serviceRepo.FirstOrDefaultAsync(x => x.Id == item.Id, ct);
                    if (trackedService is not null)
                    {
                        trackedService.Position = item.Position;
                        trackedService.UpdatedAt = DateTime.UtcNow;
                        serviceRepo.Update(trackedService);
                    }
                }

                await _uow.SaveChangesAsync(ct);

                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while reordering services.");
            }
        }
        private string? ValidateRequest<TLanguage>(IEnumerable<TLanguage> languages, string imagePath)
            where TLanguage : class
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_SERVICE_REQUEST;
            }

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return ErrorCode.SERVICE_IMAGE_REQUIRED;
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
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode);

            return new ServiceManagementDto(
                service.Id,
                service.ImagePath,
                service.Position,
                service.IsActive,
                service.ActiveStatus,
                service.CreatedAt,
                service.UpdatedAt,
                filteredLanguages.Select(x => new ServiceManagementLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.IsActive)).ToList());
        }
    }
}
