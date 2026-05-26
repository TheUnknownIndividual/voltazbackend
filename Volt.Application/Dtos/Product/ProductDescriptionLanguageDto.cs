using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Product
{
    public sealed record ProductDescriptionLanguageDto(
        LanguageCode LanguageCode,
        string Description,
        string Features);
}
