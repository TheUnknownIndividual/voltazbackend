namespace Volt.Application.Configuration
{
    public sealed class LalafoListingOptions
    {
        public bool Enabled { get; set; }
        public int DefaultCityId { get; set; } = 103261;
        public int MaxImages { get; set; } = 10;
        public string ContactUsername { get; set; } = string.Empty;
        public string ContactMobile { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public List<LalafoCategoryOption> Categories { get; set; } = new();
    }

    public sealed class LalafoCategoryOption
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        // Extra lines appended to descriptions in this category (e.g. delivery conditions).
        public List<string> Notes { get; set; } = new();
        // Field values always applied to this category; the AI is not asked about these fields.
        public List<LalafoDefaultParam> DefaultParams { get; set; } = new();
        public List<LalafoParamOption> Params { get; set; } = new();
    }

    public sealed class LalafoDefaultParam
    {
        public int ParamId { get; set; }
        public List<int> ValueIds { get; set; } = new();
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
