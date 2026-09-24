namespace Volt.Application.Configuration
{
    public sealed class LalafoListingOptions
    {
        public bool Enabled { get; set; }
        public int DefaultCityId { get; set; } = 103261;
        public string ContactUsername { get; set; } = string.Empty;
        public string ContactMobile { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public List<LalafoCategoryOption> Categories { get; set; } = new();
    }

    public sealed class LalafoCategoryOption
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public List<LalafoParamOption> Params { get; set; } = new();
    }

    public sealed class LalafoParamOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Multi { get; set; }
        public List<LalafoParamValueOption> Values { get; set; } = new();
    }

    public sealed class LalafoParamValueOption
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }
}
