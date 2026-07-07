using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Volt.Application.Dtos.CustomerAuth
{
    public sealed record PasskeyCompleteRequest(
        [Required] string ChallengeId,
        [Required] JsonElement Credential);
}
