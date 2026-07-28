#nullable enable

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarInverter;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services;

public sealed class SolarInverterDatasheetQaService : ISolarInverterDatasheetQaService
{
    private sealed record DocumentMetadata(
        string Sha256,
        string SourceUrl,
        string DocumentKind,
        int PageCount,
        bool RequiresOcr,
        string ParsedContentJson);

    private static readonly HashSet<string> QaStatuses =
        ["not-confirmed", "hold", "confirmed"];
    private static readonly HashSet<string> SystemTypes =
        ["on-grid", "off-grid", "hybrid", "unknown"];
    private static readonly HashSet<string> Phases =
        ["single", "three", "unknown"];
    private readonly DataContext _context;
    private readonly IAdminAuditService _audit;
    private readonly ISolarInverterProductionPromotionService _promotion;

    public SolarInverterDatasheetQaService(
        DataContext context,
        IAdminAuditService audit,
        ISolarInverterProductionPromotionService promotion)
    {
        _context = context;
        _audit = audit;
        _promotion = promotion;
    }

    public async Task<ApiResponse<SolarInverterQaListDto>> GetListAsync(
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var normalizedStatus = Normalize(status);
        if (normalizedStatus is not null && !QaStatuses.Contains(normalizedStatus))
        {
            return ApiResponse<SolarInverterQaListDto>.ErrorResponse(
                "INVALID_QA_STATUS",
                "Supported QA statuses are not-confirmed, hold, and confirmed.");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        var sourceQuery = _context.SolarInverterSpecifications
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.DatasheetUrl != null && x.DatasheetContentJson != null);

        var statusCounts = await sourceQuery
            .GroupBy(x => x.QaStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);

        if (normalizedStatus is not null)
        {
            sourceQuery = sourceQuery.Where(x => x.QaStatus == normalizedStatus);
        }

        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            sourceQuery = sourceQuery.Where(x =>
                x.Product.ProductName.Contains(normalizedSearch) ||
                x.ModelLabel.Contains(normalizedSearch) ||
                x.TechnicalPower.Contains(normalizedSearch));
        }

        var totalCount = await sourceQuery.CountAsync(ct);
        var specifications = await sourceQuery
            .OrderBy(x => x.QaDoneAt.HasValue)
            .ThenBy(x => x.QaStatus == "hold" ? 0 : x.QaStatus == "not-confirmed" ? 1 : 2)
            .ThenBy(x => x.Product.ProductName)
            .ThenBy(x => x.NominalAcKw)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var hashes = specifications
            .Select(x => TryGetDocumentSha256(x.DatasheetContentJson))
            .Where(x => x is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var documentMetadata = await _context.SolarInverterDatasheetDocuments
            .AsNoTracking()
            .Where(x => hashes.Contains(x.Sha256))
            .Select(x => new DocumentMetadata(
                x.Sha256,
                x.SourceUrl,
                x.DocumentKind,
                x.PageCount,
                x.RequiresOcr,
                x.ParsedContentJson))
            .ToListAsync(ct);
        var documentsByHash = documentMetadata.ToDictionary(
            x => x.Sha256,
            StringComparer.OrdinalIgnoreCase);

        var items = specifications.Select(specification =>
        {
            var sha256 = TryGetDocumentSha256(specification.DatasheetContentJson);
            documentsByHash.TryGetValue(sha256 ?? string.Empty, out var document);
            return new SolarInverterQaListItemDto(
                specification.Id,
                specification.ProductId,
                specification.Product.ProductName,
                specification.TechnicalPower,
                specification.ModelLabel,
                specification.SystemType,
                specification.Phase,
                specification.QaStatus,
                document?.SourceUrl ?? specification.DatasheetUrl,
                GetSourceUrls(document?.SourceUrl, document?.ParsedContentJson),
                document?.DocumentKind,
                document?.RequiresOcr ?? false,
                !string.IsNullOrWhiteSpace(specification.CorrectedExtractedText),
                specification.QaReviewedAt,
                specification.QaDoneAt,
                specification.ProductionPromotedAt);
        }).ToList();

        return ApiResponse<SolarInverterQaListDto>.SuccessResponse(
            new SolarInverterQaListDto(
                items,
                page,
                pageSize,
                totalCount,
                statusCounts));
    }

    public async Task<ApiResponse<SolarInverterQaDetailDto>> GetDetailAsync(
        int specificationId,
        CancellationToken ct = default)
    {
        var specification = await _context.SolarInverterSpecifications
            .AsNoTracking()
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == specificationId, ct);
        if (specification is null)
        {
            return ApiResponse<SolarInverterQaDetailDto>.ErrorResponse(
                "QA_DATASET_NOT_FOUND",
                "The inverter QA dataset was not found.");
        }

        var document = await FindDocumentAsync(specification.DatasheetContentJson, ct);
        return ApiResponse<SolarInverterQaDetailDto>.SuccessResponse(
            ToDetailDto(specification, document));
    }

    public async Task<ApiResponse<SolarInverterQaDetailDto>> UpdateAsync(
        int specificationId,
        int adminUserId,
        SolarInverterQaUpdateRequest request,
        CancellationToken ct = default)
    {
        request ??= new SolarInverterQaUpdateRequest();
        var normalizedStatus = Normalize(request.QaStatus) ?? "not-confirmed";
        if (!QaStatuses.Contains(normalizedStatus))
        {
            return ApiResponse<SolarInverterQaDetailDto>.ErrorResponse(
                "INVALID_QA_STATUS",
                "Supported QA statuses are not-confirmed, hold, and confirmed.");
        }

        if (request.Dataset is null)
        {
            return ApiResponse<SolarInverterQaDetailDto>.ErrorResponse(
                "QA_DATASET_REQUIRED",
                "An editable inverter dataset is required.");
        }

        var validationError = ValidateDataset(request.Dataset);
        if (validationError is not null)
        {
            return ApiResponse<SolarInverterQaDetailDto>.ErrorResponse(
                "INVALID_QA_DATASET",
                validationError);
        }

        if (request.QaNotes?.Length > 2000)
        {
            return ApiResponse<SolarInverterQaDetailDto>.ErrorResponse(
                "QA_NOTES_TOO_LONG",
                "QA notes cannot exceed 2,000 characters.");
        }

        if (request.CorrectedExtractedText?.Length > 2_000_000)
        {
            return ApiResponse<SolarInverterQaDetailDto>.ErrorResponse(
                "CORRECTED_TEXT_TOO_LONG",
                "Corrected extracted text cannot exceed 2,000,000 characters.");
        }

        var specification = await _context.SolarInverterSpecifications
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == specificationId, ct);
        if (specification is null)
        {
            return ApiResponse<SolarInverterQaDetailDto>.ErrorResponse(
                "QA_DATASET_NOT_FOUND",
                "The inverter QA dataset was not found.");
        }

        if (!NormalizeTechnicalPower(specification.TechnicalPower).Equals(
                NormalizeTechnicalPower(request.Dataset.TechnicalPower),
                StringComparison.Ordinal))
        {
            return ApiResponse<SolarInverterQaDetailDto>.ErrorResponse(
                "TECHNICAL_POWER_IMMUTABLE",
                "TechnicalPower identifies the database variant and cannot be changed during QA.");
        }

        ApplyDataset(specification, request.Dataset);
        specification.QaStatus = normalizedStatus;
        specification.QaNotes = NullIfWhiteSpace(request.QaNotes);
        specification.CorrectedExtractedText = NullIfWhiteSpace(request.CorrectedExtractedText);
        specification.QaReviewedAt = DateTime.UtcNow;
        specification.QaReviewedByAdminId = adminUserId;
        specification.QaDoneAt = null;
        specification.ProductionPromotedAt = null;
        specification.ProductionPromotionMessage = null;
        specification.DatasheetReviewedAt =
            normalizedStatus == "confirmed" ? specification.QaReviewedAt : null;

        await _context.SaveChangesAsync(ct);
        await _audit.WriteAsync(
            adminUserId,
            null,
            "INVERTER_DATASHEET_QA_UPDATED",
            "SolarInverterSpecification",
            specification.Id.ToString(),
            $"{specification.Product.ProductName} / {specification.TechnicalPower} / {normalizedStatus}",
            true,
            ct);

        var document = await FindDocumentAsync(specification.DatasheetContentJson, ct);
        return ApiResponse<SolarInverterQaDetailDto>.SuccessResponse(
            ToDetailDto(specification, document));
    }

    public async Task<ApiResponse<SolarInverterQaDoneDto>> DoneAsync(
        int specificationId,
        int adminUserId,
        CancellationToken ct = default)
    {
        var specification = await _context.SolarInverterSpecifications
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == specificationId, ct);
        if (specification is null)
        {
            return ApiResponse<SolarInverterQaDoneDto>.ErrorResponse(
                "QA_DATASET_NOT_FOUND",
                "The inverter QA dataset was not found.");
        }

        if (specification.QaStatus != "confirmed")
        {
            return ApiResponse<SolarInverterQaDoneDto>.ErrorResponse(
                "QA_CONFIRMATION_REQUIRED",
                "Only a confirmed dataset can be marked Done.");
        }

        var document = await FindDocumentAsync(specification.DatasheetContentJson, ct);
        var promotionResult = await _promotion.PromoteAsync(
            specification,
            specification.Product,
            document,
            ct);
        specification.QaDoneAt = DateTime.UtcNow;
        specification.ProductionPromotedAt = promotionResult.PromotedAtUtc;
        specification.ProductionPromotionMessage = Truncate(promotionResult.Message, 1000);
        await _context.SaveChangesAsync(ct);
        await _audit.WriteAsync(
            adminUserId,
            null,
            promotionResult.Promoted
                ? "INVERTER_DATASET_PROMOTED"
                : "INVERTER_DATASET_READY_FOR_PROMOTION",
            "SolarInverterSpecification",
            specification.Id.ToString(),
            promotionResult.Message,
            !promotionResult.Enabled || promotionResult.Promoted,
            ct);

        return ApiResponse<SolarInverterQaDoneDto>.SuccessResponse(
            new SolarInverterQaDoneDto(
                true,
                promotionResult.Enabled,
                promotionResult.Promoted,
                promotionResult.Message,
                promotionResult.PromotedAtUtc));
    }

    private async Task<SolarInverterDatasheetDocument?> FindDocumentAsync(
        string? catalogProvenanceJson,
        CancellationToken ct)
    {
        var sha256 = TryGetDocumentSha256(catalogProvenanceJson);
        return sha256 is null
            ? null
            : await _context.SolarInverterDatasheetDocuments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Sha256 == sha256, ct);
    }

    private static SolarInverterQaDetailDto ToDetailDto(
        SolarInverterSpecification specification,
        SolarInverterDatasheetDocument? document)
        => new(
            specification.Id,
            specification.ProductId,
            specification.Product.ProductName,
            specification.QaStatus,
            specification.QaNotes,
            specification.QaReviewedAt,
            specification.QaReviewedByAdminId,
            specification.QaDoneAt,
            specification.ProductionPromotedAt,
            specification.ProductionPromotionMessage,
            document?.SourceUrl ?? specification.DatasheetUrl,
            GetSourceUrls(document?.SourceUrl, document?.ParsedContentJson),
            document?.Sha256,
            document?.DocumentKind,
            document?.PageCount,
            document?.RequiresOcr ?? false,
            document?.ExtractedText ?? string.Empty,
            specification.CorrectedExtractedText,
            document?.ParsedContentJson,
            specification.DatasheetContentJson,
            ToDatasetDto(specification));

    private static IReadOnlyList<string> GetSourceUrls(
        string? fallbackSourceUrl,
        string? parsedContentJson)
    {
        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(parsedContentJson))
        {
            try
            {
                using var json = JsonDocument.Parse(parsedContentJson);
                if (json.RootElement.TryGetProperty("sourceAssets", out var sourceAssets)
                    && sourceAssets.ValueKind == JsonValueKind.Array)
                {
                    urls.AddRange(sourceAssets
                        .EnumerateArray()
                        .Where(x => x.TryGetProperty("sourceUrl", out _))
                        .Select(x => x.GetProperty("sourceUrl").GetString())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Cast<string>());
                }
            }
            catch (JsonException)
            {
                // Older extraction metadata falls back to the immutable primary URL.
            }
        }

        if (urls.Count == 0 && !string.IsNullOrWhiteSpace(fallbackSourceUrl))
        {
            urls.Add(fallbackSourceUrl);
        }

        return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static SolarInverterQaDatasetDto ToDatasetDto(
        SolarInverterSpecification x)
        => new(
            x.TechnicalPower,
            x.ModelLabel,
            x.SystemType,
            x.Phase,
            x.NominalAcKw,
            x.MaxDcKw,
            x.MpptCount,
            x.InputCount,
            x.MpptRange,
            x.MaxDcVoltage,
            x.MaxInputCurrent,
            x.Manufacturer,
            x.RegionalGridVersion,
            x.DatasheetRevision,
            x.MaxAcApparentPowerKva,
            x.MaxAcOutputCurrentA,
            x.NominalAcVoltageV,
            x.SupportedGridVoltageRange,
            x.SupportedFrequencyRange,
            x.StartVoltageV,
            x.MpptMinVoltageV,
            x.MpptMaxVoltageV,
            x.NominalDcVoltageV,
            x.StringInputsPerMppt,
            x.MaxOperatingCurrentPerStringA,
            x.MaxOperatingCurrentPerMpptA,
            x.MaxShortCircuitCurrentPerStringA,
            x.MaxShortCircuitCurrentPerMpptA,
            x.HasIntegratedDcSwitch,
            x.AcSpdClass,
            x.DcSpdClass,
            x.HasAfci,
            x.RequiredGridCertifications,
            x.WarrantyYears,
            x.IsEligible);

    private static void ApplyDataset(
        SolarInverterSpecification target,
        SolarInverterQaDatasetDto source)
    {
        target.TechnicalPower = source.TechnicalPower.Trim();
        target.ModelLabel = source.ModelLabel.Trim();
        target.SystemType = source.SystemType.Trim().ToLowerInvariant();
        target.Phase = source.Phase.Trim().ToLowerInvariant();
        target.NominalAcKw = source.NominalAcKw;
        target.MaxDcKw = source.MaxDcKw;
        target.MpptCount = source.MpptCount;
        target.InputCount = source.InputCount;
        target.MpptRange = NullIfWhiteSpace(source.MpptRange);
        target.MaxDcVoltage = source.MaxDcVoltage;
        target.MaxInputCurrent = NullIfWhiteSpace(source.MaxInputCurrent);
        target.Manufacturer = NullIfWhiteSpace(source.Manufacturer);
        target.RegionalGridVersion = NullIfWhiteSpace(source.RegionalGridVersion);
        target.DatasheetRevision = NullIfWhiteSpace(source.DatasheetRevision);
        target.MaxAcApparentPowerKva = source.MaxAcApparentPowerKva;
        target.MaxAcOutputCurrentA = source.MaxAcOutputCurrentA;
        target.NominalAcVoltageV = source.NominalAcVoltageV;
        target.SupportedGridVoltageRange = NullIfWhiteSpace(source.SupportedGridVoltageRange);
        target.SupportedFrequencyRange = NullIfWhiteSpace(source.SupportedFrequencyRange);
        target.StartVoltageV = source.StartVoltageV;
        target.MpptMinVoltageV = source.MpptMinVoltageV;
        target.MpptMaxVoltageV = source.MpptMaxVoltageV;
        target.NominalDcVoltageV = source.NominalDcVoltageV;
        target.StringInputsPerMppt = source.StringInputsPerMppt;
        target.MaxOperatingCurrentPerStringA = source.MaxOperatingCurrentPerStringA;
        target.MaxOperatingCurrentPerMpptA = source.MaxOperatingCurrentPerMpptA;
        target.MaxShortCircuitCurrentPerStringA = source.MaxShortCircuitCurrentPerStringA;
        target.MaxShortCircuitCurrentPerMpptA = source.MaxShortCircuitCurrentPerMpptA;
        target.HasIntegratedDcSwitch = source.HasIntegratedDcSwitch;
        target.AcSpdClass = NullIfWhiteSpace(source.AcSpdClass);
        target.DcSpdClass = NullIfWhiteSpace(source.DcSpdClass);
        target.HasAfci = source.HasAfci;
        target.RequiredGridCertifications = NullIfWhiteSpace(source.RequiredGridCertifications);
        target.WarrantyYears = source.WarrantyYears;
        target.IsEligible = source.IsEligible;
    }

    private static string? ValidateDataset(SolarInverterQaDatasetDto x)
    {
        if (string.IsNullOrWhiteSpace(x.TechnicalPower) || x.TechnicalPower.Length > 64)
            return "TechnicalPower is required and cannot exceed 64 characters.";
        if (string.IsNullOrWhiteSpace(x.ModelLabel) || x.ModelLabel.Length > 240)
            return "ModelLabel is required and cannot exceed 240 characters.";
        if (!SystemTypes.Contains(Normalize(x.SystemType) ?? string.Empty))
            return "SystemType must be on-grid, off-grid, hybrid, or unknown.";
        if (!Phases.Contains(Normalize(x.Phase) ?? string.Empty))
            return "Phase must be single, three, or unknown.";
        if (x.NominalAcKw <= 0 || x.MaxDcKw <= 0)
            return "NominalAcKw and MaxDcKw must be greater than zero.";
        if (x.MaxDcKw < x.NominalAcKw)
            return "MaxDcKw cannot be lower than NominalAcKw.";
        if (x.MpptCount < 0 || x.InputCount < 0 || x.MaxDcVoltage < 0)
            return "MPPT, input, and voltage counts cannot be negative.";
        if (x.MpptMinVoltageV > x.MpptMaxVoltageV)
            return "MpptMinVoltageV cannot exceed MpptMaxVoltageV.";
        if (HasNegativeValue(
                x.MaxAcApparentPowerKva,
                x.MaxAcOutputCurrentA,
                x.NominalAcVoltageV,
                x.MaxOperatingCurrentPerStringA,
                x.MaxOperatingCurrentPerMpptA,
                x.MaxShortCircuitCurrentPerStringA,
                x.MaxShortCircuitCurrentPerMpptA))
            return "Engineering power, voltage, and current values cannot be negative.";
        if (HasOversizedValue(x.MpptRange, 64) ||
            HasOversizedValue(x.MaxInputCurrent, 64) ||
            HasOversizedValue(x.Manufacturer, 120) ||
            HasOversizedValue(x.RegionalGridVersion, 120) ||
            HasOversizedValue(x.DatasheetRevision, 120) ||
            HasOversizedValue(x.SupportedGridVoltageRange, 120) ||
            HasOversizedValue(x.SupportedFrequencyRange, 80) ||
            HasOversizedValue(x.AcSpdClass, 80) ||
            HasOversizedValue(x.DcSpdClass, 80) ||
            HasOversizedValue(x.RequiredGridCertifications, 1000))
            return "One or more text fields exceed their database length limit.";
        if (x.WarrantyYears is < 0 or > 100)
            return "WarrantyYears must be between 0 and 100.";
        return null;
    }

    private static string? TryGetDocumentSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            using var json = JsonDocument.Parse(value);
            return json.RootElement.TryGetProperty("document", out var document) &&
                   document.TryGetProperty("sha256", out var sha256)
                ? sha256.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int maximumLength)
        => value.Length <= maximumLength ? value : value[..maximumLength];

    private static string NormalizeTechnicalPower(string value)
        => string.Concat(value.Where(character => !char.IsWhiteSpace(character)))
            .ToLowerInvariant();

    private static bool HasNegativeValue(params decimal?[] values)
        => values.Any(value => value < 0);

    private static bool HasOversizedValue(string? value, int maximumLength)
        => value?.Length > maximumLength;
}
