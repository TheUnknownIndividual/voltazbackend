#nullable enable

namespace Volt.Application.Dtos.SolarInverter;

public sealed record SolarInverterQaDatasetDto(
    string TechnicalPower,
    string ModelLabel,
    string SystemType,
    string Phase,
    decimal NominalAcKw,
    decimal MaxDcKw,
    int? MpptCount,
    int? InputCount,
    string? MpptRange,
    int? MaxDcVoltage,
    string? MaxInputCurrent,
    string? Manufacturer,
    string? RegionalGridVersion,
    string? DatasheetRevision,
    decimal? MaxAcApparentPowerKva,
    decimal? MaxAcOutputCurrentA,
    decimal? NominalAcVoltageV,
    string? SupportedGridVoltageRange,
    string? SupportedFrequencyRange,
    int? StartVoltageV,
    int? MpptMinVoltageV,
    int? MpptMaxVoltageV,
    int? NominalDcVoltageV,
    int? StringInputsPerMppt,
    decimal? MaxOperatingCurrentPerStringA,
    decimal? MaxOperatingCurrentPerMpptA,
    decimal? MaxShortCircuitCurrentPerStringA,
    decimal? MaxShortCircuitCurrentPerMpptA,
    bool? HasIntegratedDcSwitch,
    string? AcSpdClass,
    string? DcSpdClass,
    bool? HasAfci,
    string? RequiredGridCertifications,
    int WarrantyYears,
    bool IsEligible);

public sealed record SolarInverterQaListItemDto(
    int SpecificationId,
    int ProductId,
    string ProductName,
    string TechnicalPower,
    string ModelLabel,
    string SystemType,
    string Phase,
    string QaStatus,
    string? SourceUrl,
    IReadOnlyList<string> SourceUrls,
    string? DocumentKind,
    bool RequiresOcr,
    bool HasCorrections,
    DateTime? QaReviewedAt,
    DateTime? QaDoneAt,
    DateTime? ProductionPromotedAt);

public sealed record SolarInverterQaListDto(
    IReadOnlyList<SolarInverterQaListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, int> StatusCounts);

public sealed record SolarInverterQaDetailDto(
    int SpecificationId,
    int ProductId,
    string ProductName,
    string QaStatus,
    string? QaNotes,
    DateTime? QaReviewedAt,
    int? QaReviewedByAdminId,
    DateTime? QaDoneAt,
    DateTime? ProductionPromotedAt,
    string? ProductionPromotionMessage,
    string? SourceUrl,
    IReadOnlyList<string> SourceUrls,
    string? DocumentSha256,
    string? DocumentKind,
    int? PageCount,
    bool RequiresOcr,
    string OriginalExtractedText,
    string? CorrectedExtractedText,
    string? ExtractionMetadataJson,
    string? CatalogProvenanceJson,
    SolarInverterQaDatasetDto Dataset);

public sealed class SolarInverterQaUpdateRequest
{
    public string QaStatus { get; init; } = "not-confirmed";
    public string? QaNotes { get; init; }
    public string? CorrectedExtractedText { get; init; }
    public SolarInverterQaDatasetDto? Dataset { get; init; }
}

public sealed record SolarInverterQaDoneDto(
    bool Completed,
    bool AutoPromotionEnabled,
    bool PromotedToProduction,
    string Message,
    DateTime? ProductionPromotedAt);
