#nullable enable

namespace Volt.Application.Configuration;

public sealed class SolarInverterPromotionOptions
{
    public bool Enabled { get; set; }
    public string? ProductionConnectionString { get; set; }
}
