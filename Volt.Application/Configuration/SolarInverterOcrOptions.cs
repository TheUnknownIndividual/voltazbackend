namespace Volt.Application.Configuration;

public sealed class SolarInverterOcrOptions
{
    public bool Enabled { get; set; } = true;
    public string ProductionCatalogApiBaseUrl { get; set; } = string.Empty;
    public bool AllowStagingRebuild { get; set; }
    public string PdfToPpmExecutable { get; set; } = "pdftoppm";
    public string TesseractExecutable { get; set; } = "tesseract";
    public string Languages { get; set; } = "eng";
    public int Dpi { get; set; } = 180;
    public int MaxPages { get; set; } = 40;
    public int TimeoutSeconds { get; set; } = 180;
    public int MinimumEmbeddedCharactersPerPage { get; set; } = 40;
}
