using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class PartnershipTypeLanguage
    {
        public int Id { get; set; }
        public int PartnershipTypeId { get; set; }
        public PartnershipType PartnershipType { get; set; }
        public LanguageCode LanguageCode { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
    }
}
