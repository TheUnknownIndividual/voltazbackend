using Volt.Application.Dtos;
using Volt.Application.Dtos.NewsPost;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class NewsPostService : INewsPostService
    {
        private readonly IUnitOfWork _uow;

        public NewsPostService(IUnitOfWork uow)
        {
            _uow = uow;
        }
        public async Task<ApiResponse<IReadOnlyList<NewsPostDto>>> GetAllAsyncPublic(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var newsPosts = await _uow.Repository<NewsPost>().ListNoTrackingAsync(x => x.IsActive, ct);
            var languages = await _uow.Repository<NewsPostLanguage>().ListNoTrackingAsync(ct);

            var result = newsPosts
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => MapToDto(
                    x,
                    languages.Where(l => l.NewsPostId == x.Id).OrderBy(l => l.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<NewsPostDto>>.SuccessResponse(result);
        }
        public async Task<ApiResponse<IReadOnlyList<NewsPostDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var newsPosts = await _uow.Repository<NewsPost>().ListNoTrackingAsync(ct);
            var languages = await _uow.Repository<NewsPostLanguage>().ListNoTrackingAsync(ct);

            var result = newsPosts
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => MapToDto(
                    x,
                    languages.Where(l => l.NewsPostId == x.Id).OrderBy(l => l.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<NewsPostDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<NewsPostDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var newsPost = await _uow.Repository<NewsPost>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);
            if (newsPost is null)
            {
                return ApiResponse<NewsPostDto>.ErrorResponse(ErrorCode.NEWS_POST_NOT_FOUND, ErrorCode.NEWS_POST_NOT_FOUND);
            }

            var dto = await BuildDtoAsync(id, languageCode, ct);
            return ApiResponse<NewsPostDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<NewsPostDto>> CreateAsync(NewsPostCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request?.Languages);
            if (languageValidation is not null)
            {
                return ApiResponse<NewsPostDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                var newsPost = new NewsPost
                {
                    CoverImagePath = request.CoverImagePath.Trim(),
                    Source = request.Source.Trim(),
                    PostLink = request.PostLink.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await _uow.Repository<NewsPost>().AddAsync(newsPost, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var language in request.Languages)
                {
                    await _uow.Repository<NewsPostLanguage>().AddAsync(new NewsPostLanguage
                    {
                        NewsPostId = newsPost.Id,
                        LanguageCode = language.LanguageCode,
                        Title = language.Title.Trim(),
                        Description = language.Description.Trim(),
                        Content = language.Content.Trim(),
                        IsActive = true
                    }, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(newsPost.Id, languageCode, ct);
                return ApiResponse<NewsPostDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<NewsPostDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the news post.");
            }
        }

        public async Task<ApiResponse<NewsPostDto>> UpdateAsync(int id, NewsPostUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var postRepo = _uow.Repository<NewsPost>();
            var newsPost = await postRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (newsPost is null)
            {
                return ApiResponse<NewsPostDto>.ErrorResponse(ErrorCode.NEWS_POST_NOT_FOUND, ErrorCode.NEWS_POST_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request?.Languages);
            if (languageValidation is not null)
            {
                return ApiResponse<NewsPostDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                newsPost.CoverImagePath = request.CoverImagePath.Trim();
                newsPost.Source = request.Source.Trim();
                newsPost.PostLink = request.PostLink.Trim();
                newsPost.IsActive = request.IsActive;
                newsPost.UpdatedAt = DateTime.UtcNow;
                postRepo.Update(newsPost);

                await UpsertLanguagesAsync(id, request.Languages, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(id, languageCode, ct);
                return ApiResponse<NewsPostDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<NewsPostDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the news post.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var postRepo = _uow.Repository<NewsPost>();
            var newsPost = await postRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (newsPost is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.NEWS_POST_NOT_FOUND, ErrorCode.NEWS_POST_NOT_FOUND);
            }

            try
            {
                postRepo.Remove(newsPost);

                await _uow.SaveChangesAsync(ct);

                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the news post.");
            }
        }

        private string? ValidateLanguages<TLanguage>(IEnumerable<TLanguage>? languages)
            where TLanguage : class
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_NEWS_POST_REQUEST;
            }

            var codes = languages
                .Select(x => x switch
                {
                    NewsPostLanguageCreateRequest c => c.LanguageCode,
                    NewsPostLanguageUpdateRequest u => u.LanguageCode,
                    _ => default(LanguageCode)
                })
                .ToList();

            if (codes.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.NEWS_POST_LANGUAGE_DUPLICATE;
            }

            return null;
        }

        private async Task UpsertLanguagesAsync(int newsPostId, List<NewsPostLanguageUpdateRequest> languages, CancellationToken ct)
        {
            var languageRepo = _uow.Repository<NewsPostLanguage>();
            var existingLanguages = await languageRepo.ListNoTrackingAsync(x => x.NewsPostId == newsPostId, ct);

            foreach (var item in languages)
            {
                var existing = existingLanguages.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);
                if (existing is null)
                {
                    await languageRepo.AddAsync(new NewsPostLanguage
                    {
                        NewsPostId = newsPostId,
                        LanguageCode = item.LanguageCode,
                        Title = item.Title.Trim(),
                        Description = item.Description.Trim(),
                        Content = item.Content.Trim(),
                        IsActive = item.IsActive
                    }, ct);
                }
                else
                {
                    var tracked = await languageRepo.FirstOrDefaultAsync(x => x.Id == existing.Id, ct);
                    if (tracked is not null)
                    {
                        tracked.Title = item.Title.Trim();
                        tracked.Description = item.Description.Trim();
                        tracked.Content = item.Content.Trim();
                        tracked.IsActive = item.IsActive;
                        languageRepo.Update(tracked);
                    }
                }
            }
        }

        private async Task<NewsPostDto> BuildDtoAsync(int newsPostId, LanguageCode? languageCode, CancellationToken ct)
        {
            var newsPost = await _uow.Repository<NewsPost>().FirstOrDefaultNoTrackingAsync(x => x.Id == newsPostId, ct);
            var languages = await _uow.Repository<NewsPostLanguage>().ListNoTrackingAsync(x => x.NewsPostId == newsPostId, ct);

            return MapToDto(newsPost, languages.OrderBy(x => x.Id).ToList(), languageCode);
        }

        private static NewsPostDto MapToDto(NewsPost newsPost, IReadOnlyList<NewsPostLanguage> languages, LanguageCode? languageCode)
        {
            var filteredLanguages = languageCode is null
                ? languages
                : languages.Where(x => x.LanguageCode == languageCode).ToList();

            return new(
                newsPost.Id,
                newsPost.CoverImagePath,
                newsPost.Source,
                newsPost.PostLink,
                newsPost.IsActive,
                newsPost.CreatedAt,
                newsPost.UpdatedAt,
                filteredLanguages.Select(x => new NewsPostLanguageDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.Content,
                    x.IsActive)).ToList());
        }

        
    }
}
