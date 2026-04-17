using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Blog
{
    public sealed record BlogTranslationDto(
        int Id,
        LanguageCode LanguageCode,
        string Title,
        string Description,
        string Content,
        bool IsActive
    );
}
