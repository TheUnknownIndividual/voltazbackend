using Volt.Domain.Enums;

namespace Volt.API.Infrastructure.Localization
{
    public interface IAcceptLanguageService
    {
        LanguageCode? Resolve(string? acceptLanguageHeader);
    }
}

