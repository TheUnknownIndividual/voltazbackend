using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.CustomerAuth
{
    public sealed record CustomerUpdateRequest(
        [Required] [MaxLength(80)] string FirstName,
        [Required] [MaxLength(80)] string LastName,
        [Required] [MaxLength(40)] string Phone,
        [MaxLength(300)] string Address);
}
