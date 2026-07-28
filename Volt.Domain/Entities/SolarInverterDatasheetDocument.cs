namespace Volt.Domain.Entities;

public sealed class SolarInverterDatasheetDocument
{
    public int Id { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string ParserVersion { get; set; } = string.Empty;
    public string DocumentKind { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public bool RequiresOcr { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public string ParsedContentJson { get; set; } = string.Empty;
    public DateTime ImportedAtUtc { get; set; }
}
