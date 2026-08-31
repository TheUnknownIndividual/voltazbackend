using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Product
{
    public sealed class ProductParametrLanguageCreateRequest
    {
        public LanguageCode LanguageCode { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Features { get; set; } = string.Empty;
    }
}
