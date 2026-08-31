namespace Volt.Application.Dtos.NewsPost
{
    public sealed record NewsPostDto(
        int Id,
        string CoverImagePath,
        int CoverImagePositionX,
        int CoverImagePositionY,
        decimal CoverImageZoom,
        string Source,
        string PostLink,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<NewsPostLanguageDto> Languages
    );
}
