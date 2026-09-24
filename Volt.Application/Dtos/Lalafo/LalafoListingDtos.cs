namespace Volt.Application.Dtos.Lalafo
{
    public sealed class LalafoPrepareRequest
    {
        public int ProductId { get; set; }
    }

    public sealed class LalafoParamSelection
    {
        public int ParamId { get; set; }
        public List<int> ValueIds { get; set; } = new();
    }

    public sealed class LalafoContact
    {
        public string Username { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public sealed class LalafoListingPayload
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryLabel { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? Price { get; set; }
        public string Currency { get; set; } = "AZN";
        public int CityId { get; set; }
        public List<LalafoParamSelection> Params { get; set; } = new();
        public List<string> ImageUrls { get; set; } = new();
        public LalafoContact Contact { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public sealed class LalafoPreparedListingDto
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public LalafoListingPayload Payload { get; set; } = new();
    }

    public sealed class LalafoSettingsDto
    {
        public bool Enabled { get; set; }
        public List<LalafoCategorySummaryDto> Categories { get; set; } = new();
    }

    public sealed class LalafoCategorySummaryDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
