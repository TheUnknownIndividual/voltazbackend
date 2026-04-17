using Volt.Domain.Enums;

namespace Volt.Application.Dtos.NewsPost
{
    public sealed record NewsPostLanguageDto(
        int Id,
        LanguageCode LanguageCode,
        string Title,
        string Description,
        string Content,
        bool IsActive
    );
}
