namespace Volt.Domain.Entities;

public sealed class SolarInverterSpecification
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string TechnicalPower { get; set; } = string.Empty;
    public string ModelLabel { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public decimal NominalAcKw { get; set; }
    public decimal MaxDcKw { get; set; }
    public int? MpptCount { get; set; }
    public int? InputCount { get; set; }
    public string? MpptRange { get; set; }
    public int? MaxDcVoltage { get; set; }
    public string? MaxInputCurrent { get; set; }
    // Datasheet provenance. Values remain null until a reviewer imports the
    // exact manufacturer revision; the recommendation engine must not infer them.
    public string? Manufacturer { get; set; }
    public string? RegionalGridVersion { get; set; }
    public string? DatasheetUrl { get; set; }
    public string? DatasheetRevision { get; set; }
    public string? DatasheetContentJson { get; set; }
    public DateTime? DatasheetReviewedAt { get; set; }
    public decimal? MaxAcApparentPowerKva { get; set; }
    public decimal? MaxAcOutputCurrentA { get; set; }
    public decimal? NominalAcVoltageV { get; set; }
    public string? SupportedGridVoltageRange { get; set; }
    public string? SupportedFrequencyRange { get; set; }
    public int? StartVoltageV { get; set; }
    public int? MpptMinVoltageV { get; set; }
    public int? MpptMaxVoltageV { get; set; }
    public int? NominalDcVoltageV { get; set; }
    public int? StringInputsPerMppt { get; set; }
    public decimal? MaxOperatingCurrentPerStringA { get; set; }
    public decimal? MaxOperatingCurrentPerMpptA { get; set; }
    public decimal? MaxShortCircuitCurrentPerStringA { get; set; }
    public decimal? MaxShortCircuitCurrentPerMpptA { get; set; }
    public bool? HasIntegratedDcSwitch { get; set; }
    public string? AcSpdClass { get; set; }
    public string? DcSpdClass { get; set; }
    public bool? HasAfci { get; set; }
    public string? RequiredGridCertifications { get; set; }
    public string QaStatus { get; set; } = "not-confirmed";
    public string? QaNotes { get; set; }
    public string? CorrectedExtractedText { get; set; }
    public DateTime? QaReviewedAt { get; set; }
    public int? QaReviewedByAdminId { get; set; }
    public DateTime? QaDoneAt { get; set; }
    public DateTime? ProductionPromotedAt { get; set; }
    public string? ProductionPromotionMessage { get; set; }
    public int WarrantyYears { get; set; }
    public bool IsEligible { get; set; }
}
