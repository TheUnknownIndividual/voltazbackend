namespace Volt.Application.Configuration;

/// <summary>Bind only from protected environment/server configuration, never source control.</summary>
public sealed class GoogleReviewsOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string PlaceId { get; set; } = string.Empty;
}
