using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public sealed class AdminPagePermission
    {
        public int Id { get; set; }
        public int AdminUserId { get; set; }
        public AdminUser AdminUser { get; set; }
        public AdminPage Page { get; set; }
    }
}
