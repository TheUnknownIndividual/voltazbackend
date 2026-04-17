namespace Volt.Application.Dtos.NewsPost
{
    public sealed record NewsPostDto(
        int Id,
        string CoverImagePath,
        string Source,
        string PostLink,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<NewsPostLanguageDto> Languages
    );
}
