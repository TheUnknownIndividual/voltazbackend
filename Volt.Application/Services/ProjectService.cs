using Volt.Application.Dtos;
using Volt.Application.Dtos.Project;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class ProjectService : IProjectService
    {
        private readonly IUnitOfWork _uow;

        public ProjectService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<ProjectDto>> CreateAsync(ProjectCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<ProjectDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                int nextPosition = (await _uow.Repository<Project>().MaxAsync(x => x.Position, ct)) + 1;

                var projectRepo = _uow.Repository<Project>();
                var project = new Project
                {
                    Position = nextPosition,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await projectRepo.AddAsync(project, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var item in request.Languages)
                {
                    await _uow.Repository<ProjectLanguage>().AddAsync(new ProjectLanguage
                    {
                        ProjectId = project.Id,
                        LanguageCode = item.LanguageCode,
                        Title = item.Title.Trim(),
                        Description = item.Description.Trim(),
                        IsActive = true
                    }, ct);
                }

                await CreateImagesAsync(project.Id, request.ImagePaths, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildProjectDtoAsync(project.Id, languageCode, ct);
                return ApiResponse<ProjectDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<ProjectDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the project.");
            }
        }

        public async Task<ApiResponse<IReadOnlyList<ProjectDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var projects = await _uow.Repository<Project>().ListNoTrackingAsync(x => x.IsActive, ct);
            var languages = await _uow.Repository<ProjectLanguage>().ListNoTrackingAsync(ct);
            var images = await _uow.Repository<ProjectImage>().ListNoTrackingAsync(ct);

            var result = projects
                .OrderBy(x => x.Position)
                .Select(x => MapToProjectDto(
                    x,
                    languages.Where(l => l.ProjectId == x.Id).OrderBy(l => l.Id).ToList(),
                    images.Where(i => i.ProjectId == x.Id).OrderBy(i => i.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<ProjectDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<ProjectDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var project = await _uow.Repository<Project>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (project is null)
            {
                return ApiResponse<ProjectDto>.ErrorResponse(
                    ErrorCode.PROJECT_NOT_FOUND,
                    ErrorCode.PROJECT_NOT_FOUND);
            }

            var dto = await BuildProjectDtoAsync(id, languageCode, ct);
            return ApiResponse<ProjectDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<ProjectDto>> UpdateAsync(int id, ProjectUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var projectRepo = _uow.Repository<Project>();
            var project = await projectRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (project is null)
            {
                return ApiResponse<ProjectDto>.ErrorResponse(
                    ErrorCode.PROJECT_NOT_FOUND,
                    ErrorCode.PROJECT_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request.Languages.Select(x => x.LanguageCode).ToList());
            if (languageValidation is not null)
            {
                return ApiResponse<ProjectDto>.ErrorResponse(languageValidation, languageValidation);
            }

            if (request.DeleteImageIds is not null && request.DeleteImageIds.Any())
            {
                var matchedImages = await _uow.Repository<ProjectImage>()
                    .ListNoTrackingAsync(x => x.ProjectId == id && request.DeleteImageIds.Contains(x.Id), ct);

                if (matchedImages.Count != request.DeleteImageIds.Count)
                {
                    return ApiResponse<ProjectDto>.ErrorResponse(
                        ErrorCode.PROJECT_IMAGE_NOT_FOUND,
                        "One or more images were not found for this project.");
                }
            }

            try
            {
                project.Position = request.Position;
                project.IsActive = request.IsActive;
                project.UpdatedAt = DateTime.UtcNow;

                projectRepo.Update(project);

                await UpsertLanguagesAsync(id, request.Languages, ct);
                await SoftDeleteImagesAsync(id, request.DeleteImageIds, ct);
                await CreateImagesAsync(id, request.NewImagePaths, ct);

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildProjectDtoAsync(id, languageCode, ct);
                return ApiResponse<ProjectDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<ProjectDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the project.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var projectRepo = _uow.Repository<Project>();
            var project = await projectRepo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (project is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.PROJECT_NOT_FOUND,
                    ErrorCode.PROJECT_NOT_FOUND);
            }

            try
            {
                project.IsActive = false;
                project.UpdatedAt = DateTime.UtcNow;
                projectRepo.Update(project);

                var languageRepo = _uow.Repository<ProjectLanguage>();
                var imageRepo = _uow.Repository<ProjectImage>();

                var languages = await languageRepo.ListNoTrackingAsync(x => x.ProjectId == id, ct);
                foreach (var language in languages)
                {
                    var trackedLanguage = await languageRepo.FirstOrDefaultAsync(x => x.Id == language.Id, ct);
                    if (trackedLanguage is not null)
                    {
                        trackedLanguage.IsActive = false;
                        languageRepo.Update(trackedLanguage);
                    }
                }

                var images = await imageRepo.ListNoTrackingAsync(x => x.ProjectId == id, ct);
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
                    "An error occurred while deleting the project.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> ReorderAsync(List<ProjectReorderRequest> request, CancellationToken ct = default)
        {
            if (request is null || !request.Any())
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_PROJECT_REORDER_REQUEST,
                    "Reorder request cannot be empty.");
            }

            if (request.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_PROJECT_REORDER_REQUEST,
                    "Duplicate project ids detected.");
            }

            if (request.GroupBy(x => x.Position).Any(g => g.Count() > 1))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_PROJECT_REORDER_REQUEST,
                    "Duplicate positions detected.");
            }

            var ids = request.Select(x => x.Id).ToList();
            var projectRepo = _uow.Repository<Project>();
            var projects = await projectRepo.ListNoTrackingAsync(x => ids.Contains(x.Id), ct);

            if (projects.Count != ids.Count)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_PROJECT_REORDER_REQUEST,
                    "One or more project records were not found.");
            }

            try
            {
                foreach (var item in request)
                {
                    var trackedProject = await projectRepo.FirstOrDefaultAsync(x => x.Id == item.Id, ct);
                    if (trackedProject is not null)
                    {
                        trackedProject.Position = item.Position;
                        trackedProject.UpdatedAt = DateTime.UtcNow;
                        projectRepo.Update(trackedProject);
                    }
                }

                await _uow.SaveChangesAsync(ct);
                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while reordering projects.");
            }
        }

        private string? ValidateLanguages(List<LanguageCode> languages)
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_PROJECT_REQUEST;
            }

            if (languages.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.PROJECT_LANGUAGE_DUPLICATE;
            }

            return null;
        }

        private async Task UpsertLanguagesAsync(int projectId, List<ProjectLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var languageRepo = _uow.Repository<ProjectLanguage>();
            var existingLanguages = await languageRepo.ListNoTrackingAsync(x => x.ProjectId == projectId, ct);

            foreach (var item in languages)
            {
                var existingLanguage = existingLanguages.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);
                if (existingLanguage is null)
                {
                    await languageRepo.AddAsync(new ProjectLanguage
                    {
                        ProjectId = projectId,
                        LanguageCode = item.LanguageCode,
                        Title = item.Title.Trim(),
                        Description = item.Description.Trim(),
                        IsActive = item.IsActive
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

        private async Task CreateImagesAsync(int projectId, List<string>? imagePaths, CancellationToken ct)
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

                await _uow.Repository<ProjectImage>().AddAsync(new ProjectImage
                {
                    ProjectId = projectId,
                    ImagePath = imagePath.Trim(),
                    IsActive = true
                }, ct);
            }
        }

        private async Task SoftDeleteImagesAsync(int projectId, List<int>? deleteImageIds, CancellationToken ct)
        {
            if (deleteImageIds is null || !deleteImageIds.Any())
            {
                return;
            }

            var imageRepo = _uow.Repository<ProjectImage>();
            var images = await imageRepo.ListNoTrackingAsync(
                x => x.ProjectId == projectId && deleteImageIds.Contains(x.Id),
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

        private async Task<ProjectDto> BuildProjectDtoAsync(int projectId, LanguageCode? languageCode, CancellationToken ct)
        {
            var project = await _uow.Repository<Project>().FirstOrDefaultNoTrackingAsync(x => x.Id == projectId, ct);
            var languages = await _uow.Repository<ProjectLanguage>().ListNoTrackingAsync(x => x.ProjectId == projectId, ct);
            var images = await _uow.Repository<ProjectImage>().ListNoTrackingAsync(x => x.ProjectId == projectId, ct);

            return MapToProjectDto(
                project,
                languages.OrderBy(x => x.Id).ToList(),
                images.OrderBy(x => x.Id).ToList(),
                languageCode);
        }

        private ProjectDto MapToProjectDto(
            Project project,
            IEnumerable<ProjectLanguage> languages,
            IEnumerable<ProjectImage> images,
            LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode);

            return new ProjectDto(
                project.Id,
                project.Position,
                project.IsActive,
                project.CreatedAt,
                project.UpdatedAt,
                filteredLanguages.Select(x => new ProjectLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.IsActive)).ToList(),
                images.Select(x => new ProjectImageDto(
                    x.Id,
                    x.ImagePath,
                    x.IsActive)).ToList());
        }
    }
}
