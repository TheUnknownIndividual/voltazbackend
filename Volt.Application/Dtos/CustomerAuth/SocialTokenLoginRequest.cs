using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.CustomerAuth
{
    public sealed record SocialTokenLoginRequest(
        [Required] string IdToken,
        string FirstName,
        string LastName,
        string Name);
}
