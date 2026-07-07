using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.CustomerAuth
{
    public sealed record PasskeyRegisterOptionsRequest(
        [Required] [MaxLength(80)] string FirstName,
        [Required] [MaxLength(80)] string LastName,
        [Required] [EmailAddress] [MaxLength(180)] string Email,
        [MaxLength(40)] string Phone,
        [MaxLength(300)] string Address);
}
