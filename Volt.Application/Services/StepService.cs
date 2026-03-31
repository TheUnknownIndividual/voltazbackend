using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Step;
using Volt.Application.Dtos;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class StepService : IStepService
    {
        private readonly IUnitOfWork _uow;

        public StepService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<StepDto>> CreateAsync(StepCreateRequest request, CancellationToken ct = default)
        {
            var validationError = ValidateRequest(request.Languages, request.ImagePath);
            if (validationError is not null)
            {
                return ApiResponse<StepDto>.ErrorResponse(validationError, validationError);
            }

            try
            {
                var step = new Step
                {
                    ImagePath = request.ImagePath.Trim(),
                    Position = request.Position,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _uow.Repository<Step>().AddAsync(step, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var item in request.Languages)
                {
                    await _uow.Repository<StepLanguage>().AddAsync(new StepLanguage
                    {
                        StepId = step.Id,
                        LanguageCode = item.LanguageCode,
                        Title = item.Title.Trim(),
                        Description = item.Description.Trim(),
                        IsActive = true
                    }, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildStepDtoAsync(step.Id, ct);
                return ApiResponse<StepDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<StepDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the step.");
            }
        }

        public async Task<ApiResponse<IReadOnlyList<StepDto>>> GetAllAsync(CancellationToken ct = default)
        {
            var steps = await _uow.Repository<Step>().ListNoTrackingAsync( x=> x.IsActive, ct);
            var languages = await _uow.Repository<StepLanguage>().ListNoTrackingAsync(ct);

            var result = steps
                .OrderBy(x => x.Position)
                .Select(x => MapToStepDto(
                    x,
                    languages.Where(l => l.StepId == x.Id).OrderBy(l => l.Id).ToList()))
                .ToList();

            return ApiResponse<IReadOnlyList<StepDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<StepDto>> GetByIdAsync(int id, CancellationToken ct = default)
        {
            var step = await _uow.Repository<Step>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);

            if (step is null)
            {
                return ApiResponse<StepDto>.ErrorResponse(
                    ErrorCode.STEP_NOT_FOUND,
                    ErrorCode.STEP_NOT_FOUND);
            }

            var dto = await BuildStepDtoAsync(id, ct);
            return ApiResponse<StepDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<StepDto>> UpdateAsync(int id, StepUpdateRequest request, CancellationToken ct = default)
        {
            var stepRepo = _uow.Repository<Step>();
            var step = await stepRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);

            if (step is null)
            {
                return ApiResponse<StepDto>.ErrorResponse(
                    ErrorCode.STEP_NOT_FOUND,
                    ErrorCode.STEP_NOT_FOUND);
            }

            var validationError = ValidateRequest(request.Languages, request.ImagePath);
            if (validationError is not null)
            {
                return ApiResponse<StepDto>.ErrorResponse(validationError, validationError);
            }

            try
            {
                step.ImagePath = request.ImagePath.Trim();
                step.Position = request.Position;
                step.IsActive = request.IsActive;
                step.UpdatedAt = DateTime.UtcNow;

                stepRepo.Update(step);

                await UpsertLanguagesAsync(id, request.Languages, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildStepDtoAsync(id, ct);
                return ApiResponse<StepDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<StepDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the step.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var stepRepo = _uow.Repository<Step>();
            var step = await stepRepo.FirstOrDefaultAsync(x => x.Id == id, ct);

            if (step is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.STEP_NOT_FOUND,
                    ErrorCode.STEP_NOT_FOUND);
            }

            try
            {
                step.IsActive = false;
                step.UpdatedAt = DateTime.UtcNow;
                stepRepo.Update(step);

                var languageRepo = _uow.Repository<StepLanguage>();
                var languages = await languageRepo.ListNoTrackingAsync(x => x.StepId == id, ct);

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
                    "An error occurred while deleting the step.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> ReorderAsync(List<StepReorderRequest> request, CancellationToken ct = default)
        {
            if (request is null || !request.Any())
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_STEP_REORDER_REQUEST,
                    "Reorder request cannot be empty.");
            }

            if (request.GroupBy(x => x.Id).Any(g => g.Count() > 1))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_STEP_REORDER_REQUEST,
                    "Duplicate step ids detected.");
            }

            if (request.GroupBy(x => x.Position).Any(g => g.Count() > 1))
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_STEP_REORDER_REQUEST,
                    "Duplicate positions detected.");
            }

            var ids = request.Select(x => x.Id).ToList();
            var stepRepo = _uow.Repository<Step>();
            var steps = await stepRepo.ListNoTrackingAsync(x => ids.Contains(x.Id), ct);

            if (steps.Count != ids.Count)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.INVALID_STEP_REORDER_REQUEST,
                    "One or more step records were not found.");
            }

            try
            {
                foreach (var item in request)
                {
                    var trackedStep = await stepRepo.FirstOrDefaultAsync(x => x.Id == item.Id, ct);
                    if (trackedStep is not null)
                    {
                        trackedStep.Position = item.Position;
                        trackedStep.UpdatedAt = DateTime.UtcNow;
                        stepRepo.Update(trackedStep);
                    }
                }

                await _uow.SaveChangesAsync(ct);

                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while reordering steps.");
            }
        }

        private string? ValidateRequest<TLanguage>(IEnumerable<TLanguage> languages, string imagePath)
            where TLanguage : class
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_STEP_REQUEST;
            }

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return ErrorCode.STEP_IMAGE_REQUIRED;
            }

            var languageCodes = languages
                .Select(x =>
                {
                    return x switch
                    {
                        StepLanguageCreateRequest c => c.LanguageCode,
                        StepLanguageUpdateRequest u => u.LanguageCode,
                        _ => default
                    };
                })
                .ToList();

            if (languageCodes.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.STEP_LANGUAGE_DUPLICATE;
            }

            return null;
        }

        private async Task UpsertLanguagesAsync(int stepId, List<StepLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var languageRepo = _uow.Repository<StepLanguage>();
            var existingLanguages = await languageRepo.ListNoTrackingAsync(x => x.StepId == stepId, ct);

            foreach (var item in languages)
            {
                var existingLanguage = existingLanguages.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);

                if (existingLanguage is null)
                {
                    await languageRepo.AddAsync(new StepLanguage
                    {
                        StepId = stepId,
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

        private async Task<StepDto> BuildStepDtoAsync(int stepId, CancellationToken ct)
        {
            var step = await _uow.Repository<Step>().FirstOrDefaultNoTrackingAsync(x => x.Id == stepId, ct);
            var languages = await _uow.Repository<StepLanguage>().ListNoTrackingAsync(x => x.StepId == stepId, ct);

            return MapToStepDto(step, languages.OrderBy(x => x.Id).ToList());
        }

        private StepDto MapToStepDto(Step step, IEnumerable<StepLanguage> languages)
        {
            return new StepDto(
                step.Id,
                step.ImagePath,
                step.Position,
                step.IsActive,
                step.CreatedAt,
                step.UpdatedAt,
                languages.Select(x => new StepLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.IsActive)).ToList());
        }
    }
}
