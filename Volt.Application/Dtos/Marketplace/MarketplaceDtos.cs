namespace Volt.Application.Dtos.Marketplace
{
    public static class MarketplaceNames
    {
        public const string Lalafo = "lalafo";
        public const string TapAz = "tapaz";

        public static bool IsValid(string? value) => value is Lalafo or TapAz;
    }

    public sealed class MarketplacePrepareRequest
    {
        public int ProductId { get; set; }
        public string Marketplace { get; set; } = string.Empty;
    }

    public sealed class MarketplaceReportRequest
    {
        public string ExternalId { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string Status { get; set; } = "draft";
    }

    public sealed class MarketplaceListingDto
    {
        public int ProductId { get; set; }
        public string Marketplace { get; set; } = string.Empty;
        public string ExternalId { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class MarketplaceSettingsDto
    {
        public bool LalafoEnabled { get; set; }
        public bool TapAzEnabled { get; set; }
    }

    // What the browser helper receives: marketplace-specific payload behind a one-time code.
    public sealed class MarketplacePayloadEnvelope
    {
        public string Marketplace { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public object Payload { get; set; } = new();
    }

    public sealed class MarketplacePreparedDto
    {
        public string Code { get; set; } = string.Empty;
        public string Marketplace { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
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
        public int CategoryId { get; set; }
        public string CategoryLabel { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
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

    public sealed class TapAzPropertyValue
    {
        public int PropertyId { get; set; }
        public int OptionId { get; set; }
    }

    public sealed class TapAzListingPayload
    {
        public int CategoryId { get; set; }
        public string TargetLabel { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public int? Price { get; set; }
        public int RegionId { get; set; }
        public List<TapAzPropertyValue> Properties { get; set; } = new();
        public List<string> ImageUrls { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
