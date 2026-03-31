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

        public async Task<ApiResponse<AboutDto>> CreateAsync(AboutCreateRequest request, CancellationToken ct = default)
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
                    Position = request.Position,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
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

                await CreateImagesAsync(about.Id, request.ImagePaths, ct);

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildAboutDtoAsync(about.Id, ct);
                return ApiResponse<AboutDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<AboutDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the about record.");
            }
        }

        public async Task<ApiResponse<IReadOnlyList<AboutDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var abouts = await _uow.Repository<About>().ListNoTrackingAsync(x=> x.IsActive , ct);
            var languages = await _uow.Repository<AboutLanguage>().ListNoTrackingAsync(ct);
            var images = await _uow.Repository<AboutImage>().ListNoTrackingAsync(ct);

            var result = abouts
                .OrderBy(x => x.Position)
                .Select(x => MapToAboutDto(
                    x,
                    languages.Where(l => l.AboutId == x.Id).OrderBy(l => l.Id).ToList(),
                    images.Where(i => i.AboutId == x.Id).OrderBy(i => i.Id).ToList()))
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

            var dto = await BuildAboutDtoAsync(id, ct);
            return ApiResponse<AboutDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<AboutDto>> UpdateAsync(int id, AboutUpdateRequest request, CancellationToken ct = default)
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

            if (request.DeleteImageIds is not null && request.DeleteImageIds.Any())
            {
                var matchedImages = await _uow.Repository<AboutImage>()
                    .ListNoTrackingAsync(x => x.AboutId == id && request.DeleteImageIds.Contains(x.Id), ct);

                if (matchedImages.Count != request.DeleteImageIds.Count)
                {
                    return ApiResponse<AboutDto>.ErrorResponse(
                        ErrorCode.ABOUT_IMAGE_NOT_FOUND,
                        "One or more images were not found for this about.");
                }
            }

            try
            {
                about.Position = request.Position;
                about.IsActive = request.IsActive;
                about.UpdatedAt = DateTime.UtcNow;

                aboutRepo.Update(about);

                await UpsertLanguagesAsync(id, request.Languages, ct);
                await SoftDeleteImagesAsync(id, request.DeleteImageIds, ct);
                await CreateImagesAsync(id, request.NewImagePaths, ct);

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildAboutDtoAsync(id, ct);
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
                var imageRepo = _uow.Repository<AboutImage>();

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

                var images = await imageRepo.ListNoTrackingAsync(x => x.AboutId == id, ct);
                foreach (var image in images)
                {
                    var trackedImage = await imageRepo.FirstOrDefaultAsync(x => x.Id == image.Id, ct);
                    if (trackedImage is not null)
                    {
                        trackedImage.IsActive = false;
                        imageRepo.Update(trackedImage);
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

        public async Task<ApiResponse<NoContentDto>> ReorderAsync(List<AboutReorderRequest> request, CancellationToken ct = default)
        {
            if (request is null || !request.Any())
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_ABOUT_REORDER_REQUEST,
                    "Reorder request cannot be empty.");
            }

            if (request.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_ABOUT_REORDER_REQUEST,
                    "Duplicate about ids detected.");
            }

            if (request.GroupBy(x => x.Position).Any(g => g.Count() > 1))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_ABOUT_REORDER_REQUEST,
                    "Duplicate positions detected.");
            }

            var ids = request.Select(x => x.Id).ToList();
            var aboutRepo = _uow.Repository<About>();
            var abouts = await aboutRepo.ListNoTrackingAsync(x => ids.Contains(x.Id), ct);

            if (abouts.Count != ids.Count)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_ABOUT_REORDER_REQUEST,
                    "One or more about records were not found.");
            }

            try
            {
                foreach (var item in request)
                {
                    var trackedAbout = await aboutRepo.FirstOrDefaultAsync(x => x.Id == item.Id, ct);
                    if (trackedAbout is not null)
                    {
                        trackedAbout.Position = item.Position;
                        trackedAbout.UpdatedAt = DateTime.UtcNow;
                        aboutRepo.Update(trackedAbout);
                    }
                }

                await _uow.SaveChangesAsync(ct);

                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while reordering about records.");
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

        private async Task CreateImagesAsync(int aboutId, List<string>? imagePaths, CancellationToken ct)
        {
            if (imagePaths is null || !imagePaths.Any())
            {
                return;
            }

            foreach (var imagePath in imagePaths)
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    continue;
                }

                var aboutImage = new AboutImage
                {
                    AboutId = aboutId,
                    ImagePath = imagePath.Trim(),
                    IsActive = true
                };

                await _uow.Repository<AboutImage>().AddAsync(aboutImage, ct);
            }
        }

        private async Task SoftDeleteImagesAsync(int aboutId, List<int>? deleteImageIds, CancellationToken ct)
        {
            if (deleteImageIds is null || !deleteImageIds.Any())
            {
                return;
            }

            var imageRepo = _uow.Repository<AboutImage>();
            var images = await imageRepo.ListNoTrackingAsync(
                x => x.AboutId == aboutId && deleteImageIds.Contains(x.Id),
                ct);

            foreach (var image in images)
            {
                var trackedImage = await imageRepo.FirstOrDefaultAsync(x => x.Id == image.Id, ct);
                if (trackedImage is not null)
                {
                    trackedImage.IsActive = false;
                    imageRepo.Update(trackedImage);
                }
            }
        }

        private async Task<AboutDto> BuildAboutDtoAsync(int aboutId, CancellationToken ct)
        {
            var about = await _uow.Repository<About>().FirstOrDefaultNoTrackingAsync(x => x.Id == aboutId, ct);
            var languages = await _uow.Repository<AboutLanguage>().ListNoTrackingAsync(x => x.AboutId == aboutId, ct);
            var images = await _uow.Repository<AboutImage>().ListNoTrackingAsync(x => x.AboutId == aboutId, ct);

            return MapToAboutDto(
                about,
                languages.OrderBy(x => x.Id).ToList(),
                images.OrderBy(x => x.Id).ToList());
        }

        private AboutDto MapToAboutDto(
            About about,
            IEnumerable<AboutLanguage> languages,
            IEnumerable<AboutImage> images)
        {
            return new AboutDto(
                about.Id,
                about.Position,
                about.IsActive,
                about.CreatedAt,
                about.UpdatedAt,
                languages.Select(x => new AboutLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.IsActive)).ToList(),
                images.Select(x => new AboutImageDto(
                    x.Id,
                    x.ImagePath,
                    x.IsActive)).ToList());
        }
    }
}

