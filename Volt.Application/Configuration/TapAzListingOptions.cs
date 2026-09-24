namespace Volt.Application.Configuration
{
    public sealed class TapAzListingOptions
    {
        public bool Enabled { get; set; }
        public int CategoryId { get; set; } = 588;
        public int DefaultRegionId { get; set; } = 420;
        public int MaxImages { get; set; } = 10;
        public List<TapAzTargetOption> Targets { get; set; } = new();
    }

    // One selectable tap.az destination: a fixed set of (property id -> option id) picks
    // inside the configured category, e.g. Elektrik malları > İnvertorlar.
    public sealed class TapAzTargetOption
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public List<string> Notes { get; set; } = new();
        public List<TapAzPropertyPick> Properties { get; set; } = new();
    }

    public sealed class TapAzPropertyPick
    {
        public int PropertyId { get; set; }
        public int OptionId { get; set; }
    }
}
