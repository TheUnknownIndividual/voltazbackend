using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public sealed class ProductParametrLanguage
    {
        public int Id { get; set; }
        public int ProductParametrId { get; set; }
        public ProductParametr ProductParametr { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Features { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
