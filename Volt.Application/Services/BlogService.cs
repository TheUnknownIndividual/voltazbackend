using Volt.Application.Dtos;
using Volt.Application.Dtos.Blog;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services
{
    public sealed class BlogService : IBlogService
    {
        private readonly IUnitOfWork _uow;

        public BlogService(IUnitOfWork uow)
        {
            _uow = uow;
        }

        public async Task<ApiResponse<IReadOnlyList<BlogDto>>> GetAllAsync(LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var blogs = await _uow.Repository<Blog>().ListNoTrackingAsync(x => x.IsActive, ct);
            var translations = await _uow.Repository<BlogTranslation>().ListNoTrackingAsync(ct);

            var result = blogs
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => MapToDto(
                    x,
                    translations.Where(t => t.BlogId == x.Id).OrderBy(t => t.Id).ToList(),
                    languageCode))
                .ToList();

            return ApiResponse<IReadOnlyList<BlogDto>>.SuccessResponse(result);
        }

        public async Task<ApiResponse<BlogDto>> GetByIdAsync(int id, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var blog = await _uow.Repository<Blog>().FirstOrDefaultNoTrackingAsync(x => x.Id == id && x.IsActive, ct);
            if (blog is null)
            {
                return ApiResponse<BlogDto>.ErrorResponse(ErrorCode.BLOG_NOT_FOUND, ErrorCode.BLOG_NOT_FOUND);
            }

            var dto = await BuildDtoAsync(id, languageCode, ct);
            return ApiResponse<BlogDto>.SuccessResponse(dto);
        }

        public async Task<ApiResponse<BlogDto>> CreateAsync(BlogCreateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var languageValidation = ValidateLanguages(request?.Translations);
            if (languageValidation is not null)
            {
                return ApiResponse<BlogDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                var blog = new Blog
                {
                    CoverImagePath = request.CoverImagePath.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = null
                };

                await _uow.Repository<Blog>().AddAsync(blog, ct);
                await _uow.SaveChangesAsync(ct);

                foreach (var translation in request.Translations)
                {
                    await _uow.Repository<BlogTranslation>().AddAsync(new BlogTranslation
                    {
                        BlogId = blog.Id,
                        LanguageCode = translation.LanguageCode,
                        Title = translation.Title.Trim(),
                        Description = translation.Description.Trim(),
                        Content = translation.Content.Trim(),
                        IsActive = true
                    }, ct);
                }

                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(blog.Id, languageCode, ct);
                return ApiResponse<BlogDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<BlogDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while creating the blog.");
            }
        }

        public async Task<ApiResponse<BlogDto>> UpdateAsync(int id, BlogUpdateRequest request, LanguageCode? languageCode = null, CancellationToken ct = default)
        {
            var blogRepo = _uow.Repository<Blog>();
            var blog = await blogRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (blog is null)
            {
                return ApiResponse<BlogDto>.ErrorResponse(ErrorCode.BLOG_NOT_FOUND, ErrorCode.BLOG_NOT_FOUND);
            }

            var languageValidation = ValidateLanguages(request?.Translations);
            if (languageValidation is not null)
            {
                return ApiResponse<BlogDto>.ErrorResponse(languageValidation, languageValidation);
            }

            try
            {
                blog.CoverImagePath = request.CoverImagePath.Trim();
                blog.IsActive = request.IsActive;
                blog.UpdatedAt = DateTime.UtcNow;
                blogRepo.Update(blog);

                await UpsertTranslationsAsync(id, request.Translations, ct);
                await _uow.SaveChangesAsync(ct);

                var dto = await BuildDtoAsync(id, languageCode, ct);
                return ApiResponse<BlogDto>.SuccessResponse(dto);
            }
            catch
            {
                return ApiResponse<BlogDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while updating the blog.");
            }
        }

        public async Task<ApiResponse<NoContentDto>> DeleteAsync(int id, CancellationToken ct = default)
        {
            var blogRepo = _uow.Repository<Blog>();
            var blog = await blogRepo.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (blog is null)
            {
                return ApiResponse<NoContentDto>.ErrorResponse(ErrorCode.BLOG_NOT_FOUND, ErrorCode.BLOG_NOT_FOUND);
            }

            try
            {
                blog.IsActive = false;
                blog.UpdatedAt = DateTime.UtcNow;
                blogRepo.Update(blog);

                var translationRepo = _uow.Repository<BlogTranslation>();
                var translations = await translationRepo.ListNoTrackingAsync(x => x.BlogId == id, ct);
                foreach (var translation in translations)
                {
                    var tracked = await translationRepo.FirstOrDefaultAsync(x => x.Id == translation.Id, ct);
                    if (tracked is not null)
                    {
                        tracked.IsActive = false;
                        translationRepo.Update(tracked);
                    }
                }

                await _uow.SaveChangesAsync(ct);
                return ApiResponse<NoContentDto>.SuccessResponse(null);
            }
            catch
            {
                return ApiResponse<NoContentDto>.ErrorResponse(
                    ErrorCode.SERVER_ERROR,
                    "An error occurred while deleting the blog.");
            }
        }

        private string? ValidateLanguages<TLanguage>(IEnumerable<TLanguage>? languages)
            where TLanguage : class
        {
            if (languages is null || !languages.Any())
            {
                return ErrorCode.INVALID_BLOG_REQUEST;
            }

            var codes = languages
                .Select(x => x switch
                {
                    BlogTranslationCreateRequest c => c.LanguageCode,
                    BlogTranslationUpdateRequest u => u.LanguageCode,
                    _ => default(LanguageCode)
                })
                .ToList();

            if (codes.GroupBy(x => x).Any(g => g.Count() > 1))
            {
                return ErrorCode.BLOG_LANGUAGE_DUPLICATE;
            }

            return null;
        }

        private async Task UpsertTranslationsAsync(int blogId, List<BlogTranslationUpdateRequest> translations, CancellationToken ct)
        {
            var translationRepo = _uow.Repository<BlogTranslation>();
            var existingTranslations = await translationRepo.ListNoTrackingAsync(x => x.BlogId == blogId, ct);

            foreach (var item in translations)
            {
                var existing = existingTranslations.FirstOrDefault(x => x.LanguageCode == item.LanguageCode);
                if (existing is null)
                {
                    await translationRepo.AddAsync(new BlogTranslation
                    {
                        BlogId = blogId,
                        LanguageCode = item.LanguageCode,
                        Title = item.Title.Trim(),
                        Description = item.Description.Trim(),
                        Content = item.Content.Trim(),
                        IsActive = item.IsActive
                    }, ct);
                }
                else
                {
                    var tracked = await translationRepo.FirstOrDefaultAsync(x => x.Id == existing.Id, ct);
                    if (tracked is not null)
                    {
                        tracked.Title = item.Title.Trim();
                        tracked.Description = item.Description.Trim();
                        tracked.Content = item.Content.Trim();
                        tracked.IsActive = item.IsActive;
                        translationRepo.Update(tracked);
                    }
                }
            }
        }

        private async Task<BlogDto> BuildDtoAsync(int blogId, LanguageCode? languageCode, CancellationToken ct)
        {
            var blog = await _uow.Repository<Blog>().FirstOrDefaultNoTrackingAsync(x => x.Id == blogId, ct);
            var translations = await _uow.Repository<BlogTranslation>().ListNoTrackingAsync(x => x.BlogId == blogId, ct);

            return MapToDto(blog, translations.OrderBy(x => x.Id).ToList(), languageCode);
        }

        private static BlogDto MapToDto(Blog blog, IReadOnlyList<BlogTranslation> translations, LanguageCode? languageCode)
        {
            var filteredTranslations = languageCode is null
                ? translations
                : translations.Where(x => x.LanguageCode == languageCode).ToList();

            return new(
                blog.Id,
                blog.CoverImagePath,
                blog.IsActive,
                blog.CreatedAt,
                blog.UpdatedAt,
                filteredTranslations.Select(x => new BlogTranslationDto(
                    x.Id,
                    x.LanguageCode,
                    x.Title,
                    x.Description,
                    x.Content,
                    x.IsActive)).ToList());
        }
    }
}
