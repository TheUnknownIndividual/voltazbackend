namespace Volt.Application.Dtos.SolarInverter;

public sealed class SolarInverterDatasheetImportRequest
{
    public IReadOnlyCollection<int>? ProductIds { get; init; }
    public bool DryRun { get; init; } = true;
    public bool Force { get; init; }
    public bool RebuildStaging { get; init; }
}

public sealed record SolarInverterDatasheetImportItemDto(
    int ProductId,
    string ProductName,
    string? SourceUrl,
    string Status,
    int VariantCount,
    int ParsedPageCount,
    bool RequiresOcr,
    IReadOnlyList<string> Warnings);

public sealed record SolarInverterDatasheetImportReportDto(
    bool DryRun,
    int ProductsScanned,
    int ProductsWithDocuments,
    int UniqueDocumentsParsed,
    int VariantsDiscovered,
    int SpecificationsCreated,
    int SpecificationsUpdated,
    int ProductsSkipped,
    IReadOnlyList<SolarInverterDatasheetImportItemDto> Items);
