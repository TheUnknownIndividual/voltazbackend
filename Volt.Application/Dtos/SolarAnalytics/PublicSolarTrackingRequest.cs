using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Volt.Application.Dtos.SolarAnalytics
{
    public sealed class PublicSolarTrackingRequest
    {
        [MaxLength(10)]
        public string Language { get; set; }

        [MaxLength(100)]
        public string SessionId { get; set; }

        public JsonElement Payload { get; set; }
    }
}
