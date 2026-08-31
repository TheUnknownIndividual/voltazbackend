using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Volt.Application.Dtos.SolarAnalytics
{
    public sealed class PublicSolarTrackingRequest
    {
        [MaxLength(10)]
        public string Language { get; set; }

        [MaxLength(100)]
        public string SessionId { get; set; }

        [MaxLength(100)]
        public string DeviceId { get; set; }

        [MaxLength(100)]
        public string InteractionId { get; set; }

        public DateTimeOffset? ClientOccurredAt { get; set; }

        public JsonElement Payload { get; set; }

        [JsonIgnore]
        public string ClientIpAddress { get; set; }

        [JsonIgnore]
        public string RequestUserAgent { get; set; }

        [JsonIgnore]
        public string RequestReferrer { get; set; }
    }
}
