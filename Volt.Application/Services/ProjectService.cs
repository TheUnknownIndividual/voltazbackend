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
                var offers = NormalizeOffers(request.Offers, request.TotalPower, request.PowerType);
                var primaryOffer = offers.FirstOrDefault();
                var projectRepo = _uow.Repository<Project>();
                var project = new Project
                {
                    IsActive = true,
                    TotalPower = primaryOffer is null ? request.TotalPower : (int)Math.Round(primaryOffer.Power),
                    PowerType = primaryOffer?.PowerType ?? request.PowerType,
                    AnnualProduction = request.AnnualProduction,
                    AnnualProductionType = request.AnnualProductionType,
                    SystemType = request.SystemType,
                    ContactFullName = NormalizeOptional(request.ContactFullName),
                    ContactPhone = NormalizeOptional(request.ContactPhone),
                    ProjectDate = request.ProjectDate,
                    InquiryReceivedAt = request.InquiryReceivedAt,
                    OfferSentAt = request.OfferSentAt,
                    ResponseExpectedAt = request.ResponseExpectedAt,
                    CurrentStatus = NormalizeOptional(request.CurrentStatus),
                    ShortNote = NormalizeOptional(request.ShortNote),
                    OfferAmountAzn = request.OfferAmountAzn,
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
                        Location = item.Location?.Trim(),
                        IsActive = true
                    }, ct);
                }

                await CreateImagesAsync(project.Id, request.ImagePaths, ct);
                await CreateAttachmentsAsync(project.Id, request.Attachments, ct);
                await CreateOffersAsync(project.Id, offers, ct);
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
            var attachments = await _uow.Repository<ProjectAttachment>().ListNoTrackingAsync(ct);
            var offers = await _uow.Repository<ProjectOffer>().ListNoTrackingAsync(ct);

            var result = projects
                .OrderBy(x => x.Id)
                .Select(x => MapToProjectDto(
                    x,
                    languages.Where(l => l.ProjectId == x.Id).OrderBy(l => l.Id).ToList(),
                    images.Where(i => i.ProjectId == x.Id).OrderBy(i => i.Id).ToList(),
                    attachments.Where(a => a.ProjectId == x.Id).OrderBy(a => a.Id).ToList(),
                    offers.Where(o => o.ProjectId == x.Id).OrderBy(o => o.Id).ToList(),
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

            var dto = await BuildProjectDtoAsync(id, null, ct);
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
                var offers = NormalizeOffers(request.Offers, request.TotalPower, request.PowerType);
                var primaryOffer = offers.FirstOrDefault();

                project.UpdatedAt = DateTime.UtcNow;
                project.TotalPower = primaryOffer is null ? request.TotalPower : (int)Math.Round(primaryOffer.Power);
                project.PowerType = primaryOffer?.PowerType ?? request.PowerType;
                project.AnnualProduction = request.AnnualProduction;
                project.AnnualProductionType = request.AnnualProductionType;
                project.SystemType = request.SystemType;
                project.ContactFullName = NormalizeOptional(request.ContactFullName);
                project.ContactPhone = NormalizeOptional(request.ContactPhone);
                project.ProjectDate = request.ProjectDate;
                project.InquiryReceivedAt = request.InquiryReceivedAt;
                project.OfferSentAt = request.OfferSentAt;
                project.ResponseExpectedAt = request.ResponseExpectedAt;
                project.CurrentStatus = NormalizeOptional(request.CurrentStatus);
                project.ShortNote = NormalizeOptional(request.ShortNote);
                project.OfferAmountAzn = request.OfferAmountAzn;

                projectRepo.Update(project);

                await UpsertLanguagesAsync(id, request.Languages, ct);
                if (request.ImagePaths is not null)
                {
                    await ReplaceImagesAsync(id, request.ImagePaths, ct);
                }
                else
                {
                    await SoftDeleteImagesAsync(id, request.DeleteImageIds, ct);
                    await CreateImagesAsync(id, request.NewImagePaths, ct);
                }

                await ReplaceAttachmentsAsync(id, request.Attachments, ct);
                await ReplaceOffersAsync(id, offers, ct);

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
            var project = await projectRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive == true, ct);

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
                var attachmentRepo = _uow.Repository<ProjectAttachment>();
                var offerRepo = _uow.Repository<ProjectOffer>();

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

                var attachments = await attachmentRepo.ListNoTrackingAsync(x => x.ProjectId == id, ct);
                foreach (var attachment in attachments)
                {
                    var trackedAttachment = await attachmentRepo.FirstOrDefaultAsync(x => x.Id == attachment.Id, ct);
                    if (trackedAttachment is not null)
                    {
                        trackedAttachment.IsActive = false;
                        attachmentRepo.Update(trackedAttachment);
                    }
                }

                var offers = await offerRepo.ListNoTrackingAsync(x => x.ProjectId == id, ct);
                foreach (var offer in offers)
                {
                    var trackedOffer = await offerRepo.FirstOrDefaultAsync(x => x.Id == offer.Id, ct);
                    if (trackedOffer is not null)
                    {
                        trackedOffer.IsActive = false;
                        offerRepo.Update(trackedOffer);
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
                        Location = item.Location?.Trim(),
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
                        trackedLanguage.Location = item.Location?.Trim();
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

        private async Task CreateAttachmentsAsync(int projectId, List<ProjectAttachmentRequest>? attachments, CancellationToken ct)
        {
            if (attachments is null || !attachments.Any())
            {
                return;
            }

            foreach (var attachment in attachments)
            {
                if (string.IsNullOrWhiteSpace(attachment.FilePath))
                {
                    continue;
                }

                await _uow.Repository<ProjectAttachment>().AddAsync(new ProjectAttachment
                {
                    ProjectId = projectId,
                    FilePath = attachment.FilePath.Trim(),
                    Label = NormalizeOptional(attachment.Label),
                    IsActive = true
                }, ct);
            }
        }

        private async Task CreateOffersAsync(int projectId, List<ProjectOfferRequest>? offers, CancellationToken ct)
        {
            if (offers is null || !offers.Any())
            {
                return;
            }

            foreach (var offer in offers)
            {
                if (offer.Power <= 0 || string.IsNullOrWhiteSpace(offer.AreaType))
                {
                    continue;
                }

                await _uow.Repository<ProjectOffer>().AddAsync(new ProjectOffer
                {
                    ProjectId = projectId,
                    Power = offer.Power,
                    PowerType = offer.PowerType,
                    AreaType = offer.AreaType.Trim(),
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

        private async Task ReplaceImagesAsync(int projectId, List<string>? imagePaths, CancellationToken ct)
        {
            var imageRepo = _uow.Repository<ProjectImage>();
            var currentImages = await imageRepo.ListNoTrackingAsync(x => x.ProjectId == projectId && x.IsActive, ct);
            var requestedPaths = imagePaths?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList() ?? new List<string>();

            foreach (var image in currentImages.Where(x => !requestedPaths.Contains(x.ImagePath)))
            {
                var trackedImage = await imageRepo.FirstOrDefaultAsync(x => x.Id == image.Id, ct);
                if (trackedImage is not null)
                {
                    trackedImage.IsActive = false;
                    imageRepo.Update(trackedImage);
                }
            }

            var existingPaths = currentImages.Select(x => x.ImagePath).ToHashSet();
            await CreateImagesAsync(projectId, requestedPaths.Where(x => !existingPaths.Contains(x)).ToList(), ct);
        }

        private async Task ReplaceAttachmentsAsync(int projectId, List<ProjectAttachmentRequest>? attachments, CancellationToken ct)
        {
            var attachmentRepo = _uow.Repository<ProjectAttachment>();
            var currentAttachments = await attachmentRepo.ListNoTrackingAsync(x => x.ProjectId == projectId && x.IsActive, ct);
            foreach (var attachment in currentAttachments)
            {
                var trackedAttachment = await attachmentRepo.FirstOrDefaultAsync(x => x.Id == attachment.Id, ct);
                if (trackedAttachment is not null)
                {
                    trackedAttachment.IsActive = false;
                    attachmentRepo.Update(trackedAttachment);
                }
            }

            await CreateAttachmentsAsync(projectId, attachments, ct);
        }

        private async Task ReplaceOffersAsync(int projectId, List<ProjectOfferRequest>? offers, CancellationToken ct)
        {
            var offerRepo = _uow.Repository<ProjectOffer>();
            var currentOffers = await offerRepo.ListNoTrackingAsync(x => x.ProjectId == projectId && x.IsActive, ct);
            foreach (var offer in currentOffers)
            {
                var trackedOffer = await offerRepo.FirstOrDefaultAsync(x => x.Id == offer.Id, ct);
                if (trackedOffer is not null)
                {
                    trackedOffer.IsActive = false;
                    offerRepo.Update(trackedOffer);
                }
            }

            await CreateOffersAsync(projectId, offers, ct);
        }

        private async Task<ProjectDto> BuildProjectDtoAsync(int projectId, LanguageCode? languageCode, CancellationToken ct)
        {
            var project = await _uow.Repository<Project>().FirstOrDefaultNoTrackingAsync(x => x.Id == projectId, ct);
            var languages = await _uow.Repository<ProjectLanguage>().ListNoTrackingAsync(x => x.ProjectId == projectId, ct);
            var images = await _uow.Repository<ProjectImage>().ListNoTrackingAsync(x => x.ProjectId == projectId, ct);
            var attachments = await _uow.Repository<ProjectAttachment>().ListNoTrackingAsync(x => x.ProjectId == projectId, ct);
            var offers = await _uow.Repository<ProjectOffer>().ListNoTrackingAsync(x => x.ProjectId == projectId, ct);

            return MapToProjectDto(
                project,
                languages.OrderBy(x => x.Id).ToList(),
                images.OrderBy(x => x.Id).ToList(),
                attachments.OrderBy(x => x.Id).ToList(),
                offers.OrderBy(x => x.Id).ToList(),
                languageCode);
        }

        private ProjectDto MapToProjectDto(
            Project project,
            IEnumerable<ProjectLanguage> languages,
            IEnumerable<ProjectImage> images,
            IEnumerable<ProjectAttachment> attachments,
            IEnumerable<ProjectOffer> offers,
            LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode);

            return new ProjectDto(
                project.Id,
                project.IsActive,
                project.CreatedAt,
                project.UpdatedAt,
                project.TotalPower,
                project.PowerType,
                project.AnnualProduction,
                project.AnnualProductionType,
                project.SystemType,
                project.ContactFullName,
                project.ContactPhone,
                project.ProjectDate,
                project.InquiryReceivedAt,
                project.OfferSentAt,
                project.ResponseExpectedAt,
                project.CurrentStatus,
                project.ShortNote,
                project.OfferAmountAzn,
                filteredLanguages.Select(x => new ProjectLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.Location,
                    x.IsActive)).ToList(),
                images.Where(x => x.IsActive).Select(x => new ProjectImageDto(
                    x.Id,
                    x.ImagePath,
                    x.IsActive)).ToList(),
                attachments.Where(x => x.IsActive).Select(x => new ProjectAttachmentDto(
                    x.Id,
                    x.FilePath,
                    x.Label,
                    x.IsActive)).ToList(),
                offers.Where(x => x.IsActive).Select(x => new ProjectOfferDto(
                    x.Id,
                    x.Power,
                    x.PowerType,
                    x.AreaType,
                    x.IsActive)).ToList());
        }

        private static string? NormalizeOptional(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static List<ProjectOfferRequest> NormalizeOffers(
            List<ProjectOfferRequest>? offers,
            int legacyPower,
            byte legacyPowerType)
        {
            var normalizedOffers = offers?
                .Where(x => x.Power > 0 && !string.IsNullOrWhiteSpace(x.AreaType))
                .Select(x => new ProjectOfferRequest
                {
                    Power = x.Power,
                    PowerType = x.PowerType == 0 ? legacyPowerType : x.PowerType,
                    AreaType = x.AreaType.Trim()
                })
                .ToList() ?? new List<ProjectOfferRequest>();

            if (!normalizedOffers.Any() && legacyPower > 0)
            {
                normalizedOffers.Add(new ProjectOfferRequest
                {
                    Power = legacyPower,
                    PowerType = legacyPowerType == 0 ? (byte)1 : legacyPowerType,
                    AreaType = "Dam sahəsi"
                });
            }

            return normalizedOffers;
        }
    }
}
