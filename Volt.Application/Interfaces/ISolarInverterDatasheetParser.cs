namespace Volt.Application.Interfaces;

public sealed record ParsedSolarInverterDatasheet(
    int PageCount,
    bool RequiresOcr,
    bool OcrAttempted,
    bool OcrSucceeded,
    string ExtractedText,
    string ParsedContentJson,
    string DocumentKind,
    IReadOnlyList<string> Warnings);

public interface ISolarInverterDatasheetParser
{
    Task<ParsedSolarInverterDatasheet> ParseAsync(
        byte[] pdfContent,
        string sourceUrl,
        CancellationToken ct = default);
}
