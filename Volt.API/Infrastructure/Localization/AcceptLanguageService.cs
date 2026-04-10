using Volt.Domain.Enums;

namespace Volt.API.Infrastructure.Localization
{
    public sealed class AcceptLanguageService : IAcceptLanguageService
    {
        public LanguageCode? Resolve(string? acceptLanguageHeader)
        {
            if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
                return LanguageCode.AZ;

            var token = acceptLanguageHeader.Split(',').FirstOrDefault()?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(token))
                return LanguageCode.AZ;

            var code = token.Split('-').FirstOrDefault();
            return code switch
            {
                "az" => LanguageCode.AZ,
                "en" => LanguageCode.EN,
                "ru" => LanguageCode.RU,
                "tr" => LanguageCode.TR,
                _ => LanguageCode.AZ
            };
        }
    }
}

