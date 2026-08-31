using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Product
{
    public sealed class ProductAiImportSourceRequest
    {
        public string Url { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
    }

    public sealed class ProductAiImportStartRequest
    {
        public int? ProductId { get; set; }
        public int ProductCategoryId { get; set; }
        public int ProductSubCategoryId { get; set; }
        public int ProductBrandId { get; set; }
        public int? ProductTechnologyId { get; set; }
        public int DefaultCount { get; set; }
        public decimal DefaultAmount { get; set; }
        public List<ProductAiImportSourceRequest> Sources { get; set; } = new();
    }

    public sealed record ProductAiImportLanguageDraftDto(
        LanguageCode LanguageCode,
        string Description,
        string Features);

    public sealed record ProductAiImportVariantDraftDto(
        string ModelLabel,
        string TechnicalPower,
        decimal? Effectiveness,
        int Count,
        decimal Amount,
        bool CommercialValuesConfirmed,
        IReadOnlyList<ProductAiImportLanguageDraftDto> Languages,
        IReadOnlyList<string> Evidence);

    public sealed record ProductAiImportDraftDto(
        string ProductName,
        int ProductTechnologyId,
        string ProductTechnologyName,
        bool ProductTechnologyCreated,
        IReadOnlyList<ProductAiImportVariantDraftDto> Variants,
        IReadOnlyList<string> Warnings);

    public sealed record ProductAiImportJobDto(
        Guid Id,
        string Status,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime ExpiresAt,
        ProductAiImportDraftDto? Draft,
        string? ErrorCode,
        string? ErrorMessage);

    public sealed record ProductAiImportSettingsDto(
        bool Enabled,
        string Model,
        int MaxFiles,
        long MaxCombinedBytes,
        int RequestTimeoutSeconds);

    public sealed record ProductDatasheetUploadDto(
        string Url,
        string MimeType,
        long SizeBytes,
        string FileName);
}
