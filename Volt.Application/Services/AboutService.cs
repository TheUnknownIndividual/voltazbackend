using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Application.Dtos.About;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class AboutService : IAboutService
    {
        private readonly IUnitOfWork _uow;

        public AboutService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<AboutDto>> CreateAsync(AboutCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<AboutDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                
                var aboutRepo = _uow.Repository<About>();

                var about = new About
                {
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    ImagePath = request.ImagePath,
                    UpdatedAt = null
                };

                await aboutRepo.AddAsync(about, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var item in request.Languages)
                {
                    var language = new AboutLanguage
                    {
                        AboutId = about.Id,
                        LanguageCode = item.LanguageCode,
                        Title = item.Title,
                        Description = item.Description,
                        IsActive = true
                    };

                    await _uow.Repository<AboutLanguage>().AddAsync(language, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildAboutDtoAsync(about.Id, languageCode, ct);
                return ApiResponse<AboutDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<AboutDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the about record.");
            }
        }

        public async Task<ApiResponse<IReadOnlyList<AboutDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var abouts = await _uow.Repository<About>().ListNoTrackingAsync(x=> x.IsActive , ct);
            var languages = await _uow.Repository<AboutLanguage>().ListNoTrackingAsync(ct);

            var result = abouts
                .OrderBy(x => x.Id)
                .Select(x => MapToAboutDto(
                    x,
                    languages.Where(l => l.AboutId == x.Id).OrderBy(l => l.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<AboutDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<AboutDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var about = await _uow.Repository<About>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (about is null)
            {
                return ApiResponse<AboutDto>.ErrorResponse(
                    ErrorCode.ABOUT_NOT_FOUND,
                    ErrorCode.ABOUT_NOT_FOUND);
            }

            var dto = await BuildAboutDtoAsync(id, null, ct);
            return ApiResponse<AboutDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<AboutDto>> UpdateAsync(int id, AboutUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var aboutRepo = _uow.Repository<About>();
            var about = await aboutRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (about is null)
            {
                return ApiResponse<AboutDto>.ErrorResponse(
                    ErrorCode.ABOUT_NOT_FOUND,
                    ErrorCode.ABOUT_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<AboutDto>.ErrorResponse(languageValidation, languageValidation);
            }

            

            try
            {
                about.IsActive = request.IsActive;
                about.ImagePath = request.ImagePath;
                about.UpdatedAt = DateTime.UtcNow;

                aboutRepo.Update(about);

                await UpsertLanguagesAsync(id, request.Languages, ct);

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildAboutDtoAsync(id, languageCode, ct);
                return ApiResponse<AboutDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<AboutDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the about record.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var aboutRepo = _uow.Repository<About>();
            var about = await aboutRepo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (about is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.ABOUT_NOT_FOUND,
                    ErrorCode.ABOUT_NOT_FOUND);
            }

            try
            {
                about.IsActive = false;
                about.UpdatedAt = DateTime.UtcNow;
                aboutRepo.Update(about);

                var languageRepo = _uow.Repository<AboutLanguage>();
                
                var languages = await languageRepo.ListNoTrackingAsync(x => x.AboutId == id, ct);
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

        private string? ValidateLanguages(List<LanguageCode> languages)
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_ABOUT_REQUEST;
            }

            if (languages.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.ABOUT_LANGUAGE_DUPLICATE;
            }

            return null;
        }

        private async Task UpsertLanguagesAsync(int aboutId, List<AboutLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var languageRepo = _uow.Repository<AboutLanguage>();
            var existingLanguages = await languageRepo.ListNoTrackingAsync(x => x.AboutId == aboutId, ct);

            foreach (var item in languages)
            {
                var existingLanguage = existingLanguages.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);

                if (existingLanguage is null)
                {
                    var newLanguage = new AboutLanguage
                    {
                        AboutId = aboutId,
                        LanguageCode = item.LanguageCode,
                        Title = item.Title,
                        Description = item.Description,
                        IsActive = item.IsActive
                    };

                    await languageRepo.AddAsync(newLanguage, ct);
                }
                else
                {
                    var trackedLanguage = await languageRepo.FirstOrDefaultAsync(x => x.Id == existingLanguage.Id, ct);
                    if (trackedLanguage is not null)
                    {
                        trackedLanguage.Title = item.Title;
                        trackedLanguage.Description = item.Description;
                        trackedLanguage.IsActive = item.IsActive;
                        languageRepo.Update(trackedLanguage);
                    }
                }
            }
        }

        private async Task<AboutDto> BuildAboutDtoAsync(int aboutId, LanguageCode? languageCode, CancellationToken ct)
        {
            var about = await _uow.Repository<About>().FirstOrDefaultNoTrackingAsync(x => x.Id == aboutId, ct);
            var languages = await _uow.Repository<AboutLanguage>().ListNoTrackingAsync(x => x.AboutId == aboutId, ct);

            return MapToAboutDto(
                about,
                languages.OrderBy(x => x.Id).ToList(),
                languageCode);
        }

        private AboutDto MapToAboutDto(
            About about,
            IEnumerable<AboutLanguage> languages,
            LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode);

            return new AboutDto(
                about.Id,
                about.ImagePath,
                about.IsActive,
                about.CreatedAt,
                about.UpdatedAt,
                filteredLanguages.Select(x => new AboutLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.IsActive)).ToList()
                    .ToList());
        }
    }
}

