using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Product
{
    public sealed record ProductParametrLanguageDto(
        LanguageCode LanguageCode,
        string Description,
        string Features);
}
