using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.CustomerAuth
{
    public sealed record CustomerLoginRequest(
        [Required] string Identifier,
        [Required] string Password);
}
