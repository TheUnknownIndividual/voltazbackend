using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class CustomerUser
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public Role Role { get; set; } = Role.Customer;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<CustomerExternalLogin> ExternalLogins { get; set; } = new List<CustomerExternalLogin>();
        public ICollection<CustomerPasskeyCredential> PasskeyCredentials { get; set; } = new List<CustomerPasskeyCredential>();
    }
}
