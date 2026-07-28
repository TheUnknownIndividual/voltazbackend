using System.Text.Json.Serialization;
using Volt.Application.Dtos.CustomerAuth;

namespace Volt.Application.Dtos.AuthRefresh
{
    public sealed class RefreshResponseDto
    {
        public string AccessToken { get; init; }
        public CustomerDto User { get; init; }

        [JsonIgnore]
        public string RefreshToken { get; init; }
    }
}
