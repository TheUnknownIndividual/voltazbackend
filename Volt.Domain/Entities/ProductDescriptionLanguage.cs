using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ProductDescriptionLanguage
    {
        public int Id { get; set; }
        public int ProductDescriptionId { get; set; }
        public ProductDescription ProductDescription { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string Description { get; set; }
        public string Features { get; set; }
        public bool IsActive { get; set; }
    }
}
