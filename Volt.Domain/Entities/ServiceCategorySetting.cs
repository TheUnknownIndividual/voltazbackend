using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class ServiceCategorySetting
    {
        public int Id { get; set; }
        public ServiceCategory Category { get; set; }
        public bool IsReadMoreEnabled { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
