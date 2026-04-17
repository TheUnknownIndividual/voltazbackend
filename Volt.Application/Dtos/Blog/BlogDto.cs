namespace Volt.Application.Dtos.Blog
{
    public sealed record BlogDto(
        int Id,
        string CoverImagePath,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<BlogTranslationDto> Translations
    );
}
