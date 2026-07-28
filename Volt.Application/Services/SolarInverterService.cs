using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarInverter;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services;

public sealed class SolarInverterService : ISolarInverterService
{
    private sealed class ProductionCatalogEnvelope
    {
        public bool Success { get; init; }
        public ProductionCatalogPage? Data { get; init; }
    }

    private sealed class ProductionCatalogPage
    {
        public IReadOnlyList<ProductionCatalogProduct> Items { get; init; } = [];
        public int TotalPages { get; init; }
    }

    private sealed class ProductionCatalogProduct
    {
        public int Id { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public IReadOnlyList<string> ProductDatasheet { get; init; } = [];
        public IReadOnlyList<ProductionCatalogVariant> ProductParametrs { get; init; } = [];
        public int ProductCategoryId { get; init; }
        public int ProductSubCategoryId { get; init; }
        public int ProductBrandId { get; init; }
        public int? ProductTechnologyId { get; init; }
        public bool InStock { get; init; }
        public DateTime? OutOfStockAt { get; init; }
        public bool IsActive { get; init; } = true;
    }

    private sealed class ProductionCatalogVariant
    {
        public string? TechnicalPower { get; init; }
        public decimal? Effectiveness { get; init; }
        public int? Count { get; init; }
        public decimal? Amount { get; init; }
    }

    private sealed record ParsedDatasheetAsset(
        string SourceUrl,
        string Sha256,
        ParsedSolarInverterDatasheet Parsed);

    private sealed record CatalogDescription(
        string LanguageCode,
        string Description,
        string Features);

    private static readonly HashSet<string> SystemTypes = ["on-grid", "off-grid", "hybrid"];
    private static readonly HashSet<string> Phases = ["single", "three"];
    private static readonly HashSet<string> InverterCategoryNames =
        ["inverters", "invertors", "invertorlar", "inverterler"];
    private const int MaximumDatasheetBytes = 25 * 1024 * 1024;
    private readonly IUnitOfWork _uow;
    private readonly HttpClient _httpClient;
    private readonly ISolarInverterDatasheetParser _datasheetParser;
    private readonly SolarInverterOcrOptions _ocrOptions;

    public SolarInverterService(
        IUnitOfWork uow,
        HttpClient httpClient,
        ISolarInverterDatasheetParser datasheetParser,
        IOptions<SolarInverterOcrOptions> ocrOptions)
    {
        _uow = uow;
        _httpClient = httpClient;
        _datasheetParser = datasheetParser;
        _ocrOptions = ocrOptions.Value;
    }

    public async Task<ApiResponse<IReadOnlyList<SolarInverterDto>>> GetAllAsync(
        string? systemType,
        string? phase,
        CancellationToken ct = default)
    {
        var normalizedSystemType = Normalize(systemType);
        var normalizedPhase = Normalize(phase);

        if (normalizedSystemType is not null && !SystemTypes.Contains(normalizedSystemType))
        {
            return ApiResponse<IReadOnlyList<SolarInverterDto>>.ErrorResponse(
                "INVALID_SYSTEM_TYPE",
                "Supported system types are on-grid, off-grid, and hybrid.");
        }

        if (normalizedPhase is not null && !Phases.Contains(normalizedPhase))
        {
            return ApiResponse<IReadOnlyList<SolarInverterDto>>.ErrorResponse(
                "INVALID_PHASE",
                "Supported phases are single and three.");
        }

        var products = await LoadActiveInverterProductsAsync(ct);

        if (products.Count == 0)
        {
            return ApiResponse<IReadOnlyList<SolarInverterDto>>.SuccessResponse(Array.Empty<SolarInverterDto>());
        }

        var productIds = products.Select(x => x.Id).ToHashSet();
        var subCategoryIds = products.Select(x => x.ProductSubCategoryId).ToHashSet();
        var subCategoryLanguages = await _uow.Repository<ProductSubCategoryLanguage>().ListNoTrackingAsync(
            x => x.IsActive && subCategoryIds.Contains(x.ProductSubCategoryId),
            ct);
        var subCategoryNamesById = subCategoryLanguages
            .GroupBy(x => x.ProductSubCategoryId)
            .ToDictionary(
                x => x.Key,
                x => string.Join(' ', x.Select(language => language.SubCategoryName)));
        var microSubCategoryIds = subCategoryNamesById
            .Where(x =>
                NormalizeText(x.Value).Contains("micro", StringComparison.Ordinal) ||
                NormalizeText(x.Value).Contains("mikro", StringComparison.Ordinal))
            .Select(x => x.Key)
            .ToHashSet();

        products = products
            .Where(x =>
                !NormalizeText(x.ProductName).Contains("micro", StringComparison.Ordinal) &&
                !NormalizeText(x.ProductName).Contains("mikro", StringComparison.Ordinal) &&
                !microSubCategoryIds.Contains(x.ProductSubCategoryId))
            .ToList();

        productIds = products.Select(x => x.Id).ToHashSet();
        var parameters = await _uow.Repository<ProductParametr>().ListNoTrackingAsync(
            x => productIds.Contains(x.ProductId) && x.IsActive,
            ct);
        var parametersByProductId = parameters
            .GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        List<SolarInverterSpecification> specs;
        try
        {
            specs = await _uow.Repository<SolarInverterSpecification>().ListNoTrackingAsync(
                x => productIds.Contains(x.ProductId),
                ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // ProductParametr is the authoritative calculator catalog. A pending
            // additive engineering migration must not take recommendations offline.
            specs = [];
        }
        var specsByVariant = specs
            .GroupBy(x => (x.ProductId, TechnicalPower: NormalizeTechnicalPower(x.TechnicalPower)))
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(specification => specification.IsEligible)
                    .ThenBy(specification => specification.Id)
                    .First());

        var result = products
            .SelectMany(product => (parametersByProductId.GetValueOrDefault(product.Id) ?? Array.Empty<ProductParametr>())
                .GroupBy(parameter => NormalizeTechnicalPower(parameter.TechnicalPower))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .Select(group =>
                {
                    var firstParameter = group.First();
                    var nominalAcKw = ParseTechnicalPowerKw(firstParameter.TechnicalPower);
                    specsByVariant.TryGetValue((product.Id, group.Key), out var specification);
                    var subCategoryNames = subCategoryNamesById.GetValueOrDefault(product.ProductSubCategoryId) ?? string.Empty;
                    var resolvedSystemType = ResolveSystemType(specification?.SystemType, product.ProductName, subCategoryNames);
                    var resolvedPhase = ResolvePhase(specification?.Phase, product.ProductName, subCategoryNames);
                    var availableCount = group
                        .Select(parameter => parameter.Count.GetValueOrDefault())
                        .DefaultIfEmpty(0)
                        .Max();

                    return new
                    {
                        Product = product,
                        Parameter = firstParameter,
                        Specification = specification,
                        NominalAcKw = nominalAcKw,
                        SystemType = resolvedSystemType,
                        Phase = resolvedPhase,
                        AvailableCount = availableCount
                    };
                }))
            .Where(x =>
                x.NominalAcKw > 0 &&
                x.SystemType is not null &&
                x.Phase is not null &&
                (normalizedSystemType == null || x.SystemType == normalizedSystemType) &&
                (normalizedPhase == null || x.Phase == normalizedPhase))
            .Select(x =>
            {
                return new SolarInverterDto(
                    x.Product.Id,
                    x.Specification?.Id,
                    x.Parameter.TechnicalPower?.Trim() ?? string.Empty,
                    BuildModelLabel(x.Specification?.ModelLabel ?? x.Product.ProductName, x.NominalAcKw),
                    x.Product.ProductName,
                    x.SystemType!,
                    x.Phase!,
                    x.NominalAcKw,
                    x.Specification is { MaxDcKw: > 0 }
                        ? x.Specification.MaxDcKw
                        : x.NominalAcKw * 1.5m,
                    x.Specification?.MpptCount,
                    x.Specification?.InputCount,
                    x.Specification?.MpptRange,
                    x.Specification?.MaxDcVoltage,
                    x.Specification?.MaxInputCurrent,
                    x.Specification?.Manufacturer,
                    x.Specification?.RegionalGridVersion,
                    x.Specification?.DatasheetRevision,
                    x.Specification?.DatasheetUrl,
                    x.Specification?.DatasheetReviewedAt,
                    x.Specification?.MaxAcApparentPowerKva,
                    x.Specification?.MaxAcOutputCurrentA,
                    x.Specification?.NominalAcVoltageV,
                    x.Specification?.SupportedGridVoltageRange,
                    x.Specification?.SupportedFrequencyRange,
                    x.Specification?.StartVoltageV,
                    x.Specification?.MpptMinVoltageV,
                    x.Specification?.MpptMaxVoltageV,
                    x.Specification?.NominalDcVoltageV,
                    x.Specification?.StringInputsPerMppt,
                    x.Specification?.MaxOperatingCurrentPerStringA,
                    x.Specification?.MaxOperatingCurrentPerMpptA,
                    x.Specification?.MaxShortCircuitCurrentPerStringA,
                    x.Specification?.MaxShortCircuitCurrentPerMpptA,
                    x.Specification?.HasIntegratedDcSwitch,
                    x.Specification?.AcSpdClass,
                    x.Specification?.DcSpdClass,
                    x.Specification?.HasAfci,
                    x.Specification?.RequiredGridCertifications,
                    x.Specification is not null && HasCompleteEngineeringData(x.Specification),
                    x.Specification?.WarrantyYears ?? 5,
                    x.Product.InStock && x.AvailableCount > 0,
                    x.AvailableCount);
            })
            .OrderBy(x => x.NominalAcKw)
            .ThenBy(x => x.ProductId)
            .ThenBy(x => x.SpecificationId ?? int.MaxValue)
            .ToList();

        return ApiResponse<IReadOnlyList<SolarInverterDto>>.SuccessResponse(result);
    }

    public async Task<ApiResponse<SolarInverterDatasheetImportReportDto>> ImportDatasheetsAsync(
        SolarInverterDatasheetImportRequest request,
        CancellationToken ct = default)
    {
        request ??= new SolarInverterDatasheetImportRequest();
        if (request.ProductIds is { Count: > 200 })
        {
            return ApiResponse<SolarInverterDatasheetImportReportDto>.ErrorResponse(
                "TOO_MANY_PRODUCTS",
                "At most 200 explicit product IDs can be imported in one request.");
        }

        if (request.RebuildStaging && !request.DryRun && !_ocrOptions.AllowStagingRebuild)
        {
            return ApiResponse<SolarInverterDatasheetImportReportDto>.ErrorResponse(
                "STAGING_REBUILD_DISABLED",
                "Staging rebuild is disabled for this environment.");
        }

        var productionProducts = await LoadProductionCatalogAsync(ct);
        var requestedProductIds = request.ProductIds?.ToHashSet();
        var availableTargetProducts = await LoadActiveInverterProductsAsync(ct);
        var targetProductsByName = availableTargetProducts
            .GroupBy(x => x.ProductName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
        var productionSourceByTargetId = productionProducts
            .Where(x => IsInverterProductName(x.ProductName))
            .Where(x => targetProductsByName.ContainsKey(x.ProductName.Trim()))
            .GroupBy(x => targetProductsByName[x.ProductName.Trim()].Id)
            .Where(x => x.Count() == 1)
            .ToDictionary(x => x.Key, x => x.Single());
        var products = availableTargetProducts
            .Where(x => productionSourceByTargetId.ContainsKey(x.Id))
            .Where(x => requestedProductIds is null
                || requestedProductIds.Contains(x.Id)
                || requestedProductIds.Contains(productionSourceByTargetId[x.Id].Id))
            .OrderBy(x => x.Id)
            .ToList();
        var productIds = products.Select(x => x.Id).ToHashSet();
        var subCategoryIds = products.Select(x => x.ProductSubCategoryId).ToHashSet();

        var subCategoryLanguages = await _uow.Repository<ProductSubCategoryLanguage>().ListNoTrackingAsync(
            x => x.IsActive && subCategoryIds.Contains(x.ProductSubCategoryId),
            ct);
        var subCategoryNamesById = subCategoryLanguages
            .GroupBy(x => x.ProductSubCategoryId)
            .ToDictionary(x => x.Key, x => string.Join(' ', x.Select(language => language.SubCategoryName)));
        var descriptions = await _uow.Repository<ProductDescription>().ListNoTrackingAsync(
            x => productIds.Contains(x.ProductId),
            ct);
        var descriptionIds = descriptions.Select(x => x.Id).ToHashSet();
        var descriptionProductIds = descriptions.ToDictionary(x => x.Id, x => x.ProductId);
        var descriptionLanguages = await _uow.Repository<ProductDescriptionLanguage>().ListNoTrackingAsync(
            x => x.IsActive && descriptionIds.Contains(x.ProductDescriptionId),
            ct);
        var descriptionsByProductId = descriptionLanguages
            .GroupBy(x => descriptionProductIds[x.ProductDescriptionId])
            .ToDictionary(
                x => x.Key,
                x => x.Select(language => new CatalogDescription(
                    language.LanguageCode.ToString(),
                    language.Description,
                    language.Features)).ToArray());
        var brandIds = products.Select(x => x.ProductBrandId).ToHashSet();
        var brandsById = (await _uow.Repository<ProductBrand>().ListNoTrackingAsync(
                x => brandIds.Contains(x.Id),
                ct))
            .ToDictionary(x => x.Id, x => x.Name);
        List<SolarInverterSpecification> specifications;
        try
        {
            specifications = await _uow.Repository<SolarInverterSpecification>().ListNoTrackingAsync(
                x => productIds.Contains(x.ProductId),
                ct);
        }
        catch (Exception) when (request.DryRun && !ct.IsCancellationRequested)
        {
            // A dry run can audit the source catalog before the additive ingestion
            // migration is installed. Commit mode still requires the target schema.
            specifications = [];
        }
        var specificationsByVariant = specifications
            .GroupBy(x => (x.ProductId, Power: NormalizeTechnicalPower(x.TechnicalPower)))
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Id).First());
        if (request.RebuildStaging && !request.DryRun)
        {
            await PrepareStagingRebuildAsync(specifications, ct);
        }

        // Keep only the document entities touched during this run. Datasheet image
        // bytes are released after each product to cap memory use.
        var documentsBySha =
            new Dictionary<string, SolarInverterDatasheetDocument>(StringComparer.OrdinalIgnoreCase);
        var importItems = new List<SolarInverterDatasheetImportItemDto>();
        var uniqueDocumentsParsed = 0;
        var specificationsCreated = 0;
        var specificationsUpdated = 0;
        var productsSkipped = 0;
        var productsWithDocuments = 0;
        var variantsDiscovered = 0;
        var importedAtUtc = DateTime.UtcNow;

        foreach (var product in products)
        {
            ct.ThrowIfCancellationRequested();
            var productionProduct = productionSourceByTargetId[product.Id];
            var variantGroups = productionProduct.ProductParametrs
                .GroupBy(x => NormalizeTechnicalPower(x.TechnicalPower))
                .Where(x => !string.IsNullOrWhiteSpace(x.Key) && ParseTechnicalPowerKw(x.First().TechnicalPower) > 0)
                .ToArray();
            variantsDiscovered += variantGroups.Length;
            var itemWarnings = new List<string>();

            if (variantGroups.Length == 0)
            {
                productsSkipped++;
                importItems.Add(new SolarInverterDatasheetImportItemDto(
                    product.Id,
                    product.ProductName,
                    productionProduct.ProductDatasheet.FirstOrDefault(),
                    "no-active-wattage-variants",
                    0,
                    0,
                    false,
                    ["No production sub-product choice contains a parseable W or kW value."]));
                continue;
            }

            if (!TryValidateDatasheetAssetUrls(
                    productionProduct.ProductDatasheet,
                    out var sourceUrls,
                    out var urlWarning))
            {
                productsSkipped++;
                importItems.Add(new SolarInverterDatasheetImportItemDto(
                    product.Id,
                    product.ProductName,
                    productionProduct.ProductDatasheet.FirstOrDefault(),
                    "missing-or-unsafe-datasheet-images",
                    variantGroups.Length,
                    0,
                    false,
                    [urlWarning]));
                continue;
            }

            productsWithDocuments++;
            var sourceUrl = sourceUrls[0];
            SolarInverterDatasheetDocument documentEntity;
            ParsedSolarInverterDatasheet parsedDocument;
            try
            {
                var downloadedAssets = new List<(string Url, string Sha256, byte[] Content)>(
                    sourceUrls.Count);
                foreach (var assetUrl in sourceUrls)
                {
                    var content = await DownloadDatasheetAssetAsync(assetUrl, ct);
                    downloadedAssets.Add((
                        assetUrl,
                        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                        content));
                }

                var sha256 = ComputeDatasheetSetSha256(downloadedAssets);
                SolarInverterDatasheetDocument? existingDocument = null;
                if (!request.DryRun
                    && !request.RebuildStaging
                    && !documentsBySha.TryGetValue(sha256, out existingDocument))
                {
                    existingDocument = await _uow.Repository<SolarInverterDatasheetDocument>()
                        .FirstOrDefaultNoTrackingAsync(x => x.Sha256 == sha256, ct);
                }

                if (existingDocument is not null && !request.Force)
                {
                    documentEntity = existingDocument;
                    parsedDocument = ToParsedDatasheet(existingDocument);
                    documentsBySha[sha256] = existingDocument;
                }
                else
                {
                    var parsedAssets = new List<ParsedDatasheetAsset>(downloadedAssets.Count);
                    foreach (var asset in downloadedAssets)
                    {
                        var parsedAsset = await _datasheetParser.ParseAsync(
                            asset.Content,
                            asset.Url,
                            ct);
                        if (parsedAsset.DocumentKind == "certificate")
                        {
                            throw new InvalidDataException(
                                "A displayed datasheet asset was detected as a certificate and was excluded.");
                        }

                        parsedAssets.Add(new ParsedDatasheetAsset(
                            asset.Url,
                            asset.Sha256,
                            parsedAsset));
                        uniqueDocumentsParsed++;
                    }

                    parsedDocument = CombineParsedDatasheetAssets(parsedAssets);
                    documentEntity = existingDocument ?? new SolarInverterDatasheetDocument();
                    ApplyParsedDocument(
                        documentEntity,
                        sourceUrl,
                        sha256,
                        parsedDocument,
                        importedAtUtc);
                    if (!request.DryRun)
                    {
                        if (existingDocument is null)
                        {
                            await _uow.Repository<SolarInverterDatasheetDocument>()
                                .AddAsync(documentEntity, ct);
                        }
                        else
                        {
                            _uow.Repository<SolarInverterDatasheetDocument>()
                                .Update(documentEntity);
                        }
                    }

                    documentsBySha[sha256] = documentEntity;
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                productsSkipped++;
                importItems.Add(new SolarInverterDatasheetImportItemDto(
                    product.Id,
                    product.ProductName,
                    sourceUrl,
                    "document-parse-failed",
                    variantGroups.Length,
                    0,
                    false,
                    [error.Message]));
                continue;
            }

            itemWarnings.AddRange(parsedDocument.Warnings);
            var subCategoryNames =
                subCategoryNamesById.GetValueOrDefault(product.ProductSubCategoryId) ?? string.Empty;
            var isMicro = NormalizeText($"{product.ProductName} {subCategoryNames}")
                .Contains("micro", StringComparison.Ordinal) ||
                NormalizeText($"{product.ProductName} {subCategoryNames}")
                    .Contains("mikro", StringComparison.Ordinal);

            foreach (var variantGroup in variantGroups)
            {
                var parameter = variantGroup.First();
                var normalizedPower = variantGroup.Key;
                var nominalAcKw = ParseTechnicalPowerKw(parameter.TechnicalPower);
                specificationsByVariant.TryGetValue((product.Id, normalizedPower), out var specification);
                var isNew = specification is null;
                specification ??= new SolarInverterSpecification
                {
                    ProductId = product.Id,
                    TechnicalPower = parameter.TechnicalPower?.Trim() ?? string.Empty,
                    WarrantyYears = 5
                };

                if (specification.DatasheetReviewedAt.HasValue && !request.Force)
                {
                    itemWarnings.Add(
                        $"{specification.TechnicalPower}: reviewed engineering values were preserved.");
                    continue;
                }

                specification.ModelLabel = BuildModelLabel(product.ProductName, nominalAcKw);
                specification.SystemType =
                    ResolveSystemType(specification.SystemType, product.ProductName, subCategoryNames) ??
                    "unknown";
                specification.Phase =
                    ResolvePhase(specification.Phase, product.ProductName, subCategoryNames) ?? "unknown";
                specification.NominalAcKw = nominalAcKw;
                if (specification.MaxDcKw <= 0)
                {
                    specification.MaxDcKw = nominalAcKw * 1.5m;
                }
                specification.IsEligible = !isMicro && SystemTypes.Contains(specification.SystemType);
                specification.DatasheetUrl = sourceUrl;
                specification.DatasheetContentJson = BuildVariantSourceJson(
                    product,
                    brandsById.GetValueOrDefault(product.ProductBrandId),
                    subCategoryNames,
                    variantGroup,
                    descriptionsByProductId.GetValueOrDefault(product.Id) ??
                    Array.Empty<CatalogDescription>(),
                    productionProduct,
                    documentEntity.Sha256,
                    parsedDocument,
                    importedAtUtc);
                if (!request.DryRun)
                {
                    if (isNew)
                    {
                        await _uow.Repository<SolarInverterSpecification>().AddAsync(specification, ct);
                    }
                    else
                    {
                        _uow.Repository<SolarInverterSpecification>().Update(specification);
                    }
                }

                specificationsByVariant[(product.Id, normalizedPower)] = specification;
                if (isNew)
                {
                    specificationsCreated++;
                }
                else
                {
                    specificationsUpdated++;
                }
            }

            importItems.Add(new SolarInverterDatasheetImportItemDto(
                product.Id,
                product.ProductName,
                sourceUrl,
                request.DryRun ? "dry-run-parsed" : "stored-not-confirmed",
                variantGroups.Length,
                parsedDocument.PageCount,
                parsedDocument.RequiresOcr,
                itemWarnings.Distinct().ToArray()));
        }

        if (!request.DryRun)
        {
            await _uow.SaveChangesAsync(ct);
        }

        return ApiResponse<SolarInverterDatasheetImportReportDto>.SuccessResponse(
            new SolarInverterDatasheetImportReportDto(
                request.DryRun,
                products.Count,
                productsWithDocuments,
                uniqueDocumentsParsed,
                variantsDiscovered,
                specificationsCreated,
                specificationsUpdated,
                productsSkipped,
                importItems));
    }

    private async Task<List<Product>> LoadActiveInverterProductsAsync(CancellationToken ct)
    {
        var activeCategories = await _uow.Repository<ProductCategory>()
            .ListNoTrackingAsync(x => x.IsActive, ct);
        var activeCategoryIds = activeCategories.Select(x => x.Id).ToHashSet();
        var categoryLanguages = await _uow.Repository<ProductCategoryLanguage>().ListNoTrackingAsync(
            x => x.IsActive && activeCategoryIds.Contains(x.ProductCategoryId),
            ct);
        var inverterCategoryIds = categoryLanguages
            .Where(x => InverterCategoryNames.Contains(NormalizeText(x.CategoryName)))
            .Select(x => x.ProductCategoryId)
            .ToHashSet();

        if (inverterCategoryIds.Count > 0)
        {
            return await _uow.Repository<Product>().ListNoTrackingAsync(
                x => x.IsActive && inverterCategoryIds.Contains(x.ProductCategoryId),
                ct);
        }

        var activeProducts = await _uow.Repository<Product>()
            .ListNoTrackingAsync(x => x.IsActive, ct);
        return activeProducts.Where(x => IsInverterProductName(x.ProductName)).ToList();
    }

    private async Task<IReadOnlyList<ProductionCatalogProduct>> LoadProductionCatalogAsync(
        CancellationToken ct)
    {
        if (!Uri.TryCreate(
                _ocrOptions.ProductionCatalogApiBaseUrl,
                UriKind.Absolute,
                out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps
            || !baseUri.Host.Equals("api.volt.az", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "A valid production catalog API URL is not configured for datasheet staging.");
        }

        var products = new List<ProductionCatalogProduct>();
        var page = 1;
        var totalPages = 1;
        do
        {
            var pageUri = new Uri(
                baseUri,
                $"Products?Page={page}&PageSize=100");
            var envelope = await _httpClient.GetFromJsonAsync<ProductionCatalogEnvelope>(
                pageUri,
                ct);
            if (envelope is null || !envelope.Success || envelope.Data is null)
            {
                throw new InvalidDataException(
                    $"Production catalog page {page} could not be read.");
            }

            products.AddRange(envelope.Data.Items);
            totalPages = Math.Clamp(envelope.Data.TotalPages, 1, 100);
            page++;
        } while (page <= totalPages);

        return products;
    }

    private async Task<byte[]> DownloadDatasheetAssetAsync(
        string sourceUrl,
        CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(
            sourceUrl,
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumDatasheetBytes)
        {
            throw new InvalidDataException("The datasheet image exceeds the 25 MB import limit.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        using var destination = new MemoryStream();
        var buffer = new byte[81920];
        var totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaximumDatasheetBytes)
            {
                throw new InvalidDataException("The datasheet image exceeds the 25 MB import limit.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
        }

        var content = destination.ToArray();
        if (!IsSupportedImage(content))
        {
            throw new InvalidDataException(
                "The displayed datasheet asset is not a supported image.");
        }

        return content;
    }

    private static bool TryValidateDatasheetAssetUrls(
        IReadOnlyCollection<string>? values,
        out IReadOnlyList<string> sourceUrls,
        out string warning)
    {
        sourceUrls = [];
        var candidates = (values ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (candidates.Length == 0)
        {
            warning = "No displayed ProductDatasheet image exists in the production catalog.";
            return false;
        }

        if (candidates.Length > 10)
        {
            warning = "A maximum of 10 displayed datasheet images can be processed per product.";
            return false;
        }

        var validated = new List<string>(candidates.Length);
        foreach (var candidate in candidates)
        {
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !uri.Host.Equals("cloudfiles.volt.az", StringComparison.OrdinalIgnoreCase))
            {
                warning =
                    "Only displayed HTTPS datasheet images hosted at cloudfiles.volt.az can be imported.";
                return false;
            }

            validated.Add(uri.AbsoluteUri);
        }

        sourceUrls = validated;
        warning = string.Empty;
        return true;
    }

    private static bool IsSupportedImage(byte[] content)
        => content.Length >= 12
            && ((content[0] == 0x89
                    && content[1] == 0x50
                    && content[2] == 0x4E
                    && content[3] == 0x47)
                || (content[0] == 0xFF && content[1] == 0xD8)
                || (content[0] == 0x42 && content[1] == 0x4D)
                || (content[0] == 0x49 && content[1] == 0x49
                    && content[2] == 0x2A && content[3] == 0x00)
                || (content[0] == 0x4D && content[1] == 0x4D
                    && content[2] == 0x00 && content[3] == 0x2A)
                || (Encoding.ASCII.GetString(content, 0, 4) == "RIFF"
                    && Encoding.ASCII.GetString(content, 8, 4) == "WEBP"));

    private static string ComputeDatasheetSetSha256(
        IEnumerable<(string Url, string Sha256, byte[] Content)> assets)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var asset in assets)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(asset.Url));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(asset.Sha256));
            hash.AppendData([0]);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static ParsedSolarInverterDatasheet CombineParsedDatasheetAssets(
        IReadOnlyList<ParsedDatasheetAsset> assets)
    {
        var warnings = assets
            .SelectMany(x => x.Parsed.Warnings)
            .Distinct()
            .ToArray();
        var extractedText = string.Join(
            $"{Environment.NewLine}{Environment.NewLine}",
            assets.Select((asset, index) =>
                $"=== Displayed datasheet image {index + 1}: {asset.SourceUrl} ===" +
                $"{Environment.NewLine}{asset.Parsed.ExtractedText}"));
        var requiresOcr = assets.Any(x => x.Parsed.RequiresOcr);
        var parsedContentJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 4,
            parserVersion = "volt-pdfpig-tesseract-1.2",
            documentKind = "manufacturer-datasheet-image-set",
            pageCount = assets.Count,
            requiresOcr,
            ocrAttempted = assets.Any(x => x.Parsed.OcrAttempted),
            ocrSucceeded = assets.All(x => x.Parsed.OcrSucceeded),
            sourceAssets = assets.Select((asset, index) => new
            {
                imageNumber = index + 1,
                sourceUrl = asset.SourceUrl,
                asset.Sha256,
                extraction = JsonSerializer.Deserialize<JsonElement>(
                    asset.Parsed.ParsedContentJson)
            }),
            warnings
        });

        return new ParsedSolarInverterDatasheet(
            assets.Count,
            requiresOcr,
            true,
            assets.All(x => x.Parsed.OcrSucceeded),
            extractedText,
            parsedContentJson,
            "manufacturer-datasheet-image-set",
            warnings);
    }

    private static ParsedSolarInverterDatasheet ToParsedDatasheet(
        SolarInverterDatasheetDocument document)
        => new(
            document.PageCount,
            document.RequiresOcr,
            true,
            !document.RequiresOcr,
            document.ExtractedText,
            document.ParsedContentJson,
            document.DocumentKind,
            []);

    private async Task PrepareStagingRebuildAsync(
        IEnumerable<SolarInverterSpecification> specifications,
        CancellationToken ct)
    {
        foreach (var specification in specifications)
        {
            specification.DatasheetUrl = null;
            specification.DatasheetRevision = null;
            specification.DatasheetContentJson = null;
            specification.DatasheetReviewedAt = null;
            specification.CorrectedExtractedText = null;
            specification.QaStatus = "not-confirmed";
            specification.QaNotes = null;
            specification.QaReviewedAt = null;
            specification.QaReviewedByAdminId = null;
            specification.QaDoneAt = null;
            specification.ProductionPromotedAt = null;
            specification.ProductionPromotionMessage = null;
            _uow.Repository<SolarInverterSpecification>().Update(specification);
        }

        var documents = await _uow.Repository<SolarInverterDatasheetDocument>()
            .ListNoTrackingAsync(_ => true, ct);
        foreach (var document in documents)
        {
            _uow.Repository<SolarInverterDatasheetDocument>().Remove(document);
        }
    }

    private static void ApplyParsedDocument(
        SolarInverterDatasheetDocument entity,
        string sourceUrl,
        string sha256,
        ParsedSolarInverterDatasheet parsed,
        DateTime importedAtUtc)
    {
        entity.SourceUrl = sourceUrl;
        entity.Sha256 = sha256;
        entity.ParserVersion = "volt-pdfpig-tesseract-1.2";
        entity.DocumentKind = parsed.DocumentKind;
        entity.PageCount = parsed.PageCount;
        entity.RequiresOcr = parsed.RequiresOcr;
        entity.ExtractedText = parsed.ExtractedText;
        entity.ParsedContentJson = parsed.ParsedContentJson;
        entity.ImportedAtUtc = importedAtUtc;
    }

    private static string BuildVariantSourceJson(
        Product product,
        string? brand,
        string subCategoryNames,
        IEnumerable<ProductionCatalogVariant> parameters,
        IReadOnlyCollection<CatalogDescription> descriptions,
        ProductionCatalogProduct productionProduct,
        string documentSha256,
        ParsedSolarInverterDatasheet parsedDocument,
        DateTime importedAtUtc)
        => JsonSerializer.Serialize(new
        {
            schemaVersion = 2,
            reviewStatus = "not-confirmed",
            importedAtUtc,
            document = new
            {
                sha256 = documentSha256,
                parsedDocument.DocumentKind,
                parsedDocument.PageCount,
                parsedDocument.RequiresOcr,
                warnings = parsedDocument.Warnings
            },
            product = new
            {
                product.Id,
                product.ProductName,
                product.ProductCategoryId,
                product.ProductSubCategoryId,
                subCategoryNames,
                product.ProductBrandId,
                brand,
                product.ProductTechnologyId,
                product.InStock,
                product.OutOfStockAt,
                product.IsActive,
                productionProductId = productionProduct.Id,
                productionProduct.ProductDatasheet
            },
            variants = parameters.Select(parameter => new
            {
                parameter.TechnicalPower,
                parameter.Effectiveness,
                parameter.Count,
                parameter.Amount,
                IsActive = true
            }),
            descriptions
        });

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string NormalizeTechnicalPower(string? value)
        => Regex.Replace(value?.Trim() ?? string.Empty, @"\s+", string.Empty).ToLowerInvariant();

    private static decimal ParseTechnicalPowerKw(string? value)
    {
        var normalized = NormalizeTechnicalPower(value);
        var multiplier = normalized.EndsWith("kw", StringComparison.OrdinalIgnoreCase) ? 1m : 0.001m;
        var numericValue = Regex.Replace(normalized, @"(?:kw|w)$", string.Empty, RegexOptions.IgnoreCase);

        return decimal.TryParse(numericValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed * multiplier
            : 0;
    }

    private static bool IsInverterProductName(string? productName)
        => Regex.IsMatch(productName ?? string.Empty, @"\binvert(?:er|or)\b", RegexOptions.IgnoreCase);

    private static string? ResolveSystemType(string? storedValue, string productName, string subCategoryNames)
    {
        var searchableValue = NormalizeText($"{productName} {subCategoryNames}");
        if (searchableValue.Contains("off grid", StringComparison.Ordinal) ||
            searchableValue.Contains("şəbəkədənkənar", StringComparison.Ordinal))
        {
            return "off-grid";
        }

        if (searchableValue.Contains("hybrid", StringComparison.Ordinal) ||
            searchableValue.Contains("hybird", StringComparison.Ordinal) ||
            searchableValue.Contains("hibrid", StringComparison.Ordinal))
        {
            return "hybrid";
        }

        if (searchableValue.Contains("on grid", StringComparison.Ordinal) ||
            searchableValue.Contains("şəbəkəyə qoşulan", StringComparison.Ordinal))
        {
            return "on-grid";
        }

        var normalizedStoredValue = Normalize(storedValue);
        return normalizedStoredValue is not null && SystemTypes.Contains(normalizedStoredValue)
            ? normalizedStoredValue
            : null;
    }

    private static string? ResolvePhase(string? storedValue, string productName, string subCategoryNames)
    {
        var searchableValue = NormalizeText($"{productName} {subCategoryNames}");
        if (Regex.IsMatch(
            searchableValue,
            @"(?:\b3\s*phase\b|\bthree\s*phase\b|tl3|\bmod\b|\bmid\b|\bmac\b|\bmax\b|\bwit\b)",
            RegexOptions.IgnoreCase))
        {
            return "three";
        }

        var normalizedStoredValue = Normalize(storedValue);
        return normalizedStoredValue is not null && Phases.Contains(normalizedStoredValue)
            ? normalizedStoredValue
            : "single";
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return Regex.Replace(builder.ToString(), @"[\s_-]+", " ").Trim();
    }

    private static string BuildModelLabel(string modelLabel, decimal nominalAcKw)
    {
        var label = modelLabel
            .Replace(" On Grid Inverter", " On-Grid inverter", StringComparison.OrdinalIgnoreCase)
            .Replace(" On-Grid Inverter", " On-Grid inverter", StringComparison.OrdinalIgnoreCase)
            .Replace(" Hybird Inverter", " Hybrid inverter", StringComparison.OrdinalIgnoreCase)
            .Replace(" Hybrid Inverter", " Hybrid inverter", StringComparison.OrdinalIgnoreCase);
        var selectedPower = nominalAcKw % 1 == 0
            ? nominalAcKw.ToString("0", CultureInfo.InvariantCulture)
            : nominalAcKw.ToString("0.###", CultureInfo.InvariantCulture);

        return Regex.Replace(
            label,
            @"(?<start>\d+(?:\.\d+)?)-\d+(?:\.\d+)?(?=KTL)",
            selectedPower,
            RegexOptions.IgnoreCase);
    }

    private static bool HasCompleteEngineeringData(SolarInverterSpecification specification)
        => specification.DatasheetReviewedAt.HasValue
            && !string.IsNullOrWhiteSpace(specification.DatasheetRevision)
            && !string.IsNullOrWhiteSpace(specification.DatasheetUrl)
            && specification.MaxAcApparentPowerKva.HasValue
            && specification.MaxAcOutputCurrentA.HasValue
            && specification.NominalAcVoltageV.HasValue
            && !string.IsNullOrWhiteSpace(specification.SupportedGridVoltageRange)
            && !string.IsNullOrWhiteSpace(specification.SupportedFrequencyRange)
            && specification.MaxDcVoltage.HasValue
            && specification.StartVoltageV.HasValue
            && specification.MpptMinVoltageV.HasValue
            && specification.MpptMaxVoltageV.HasValue
            && specification.NominalDcVoltageV.HasValue
            && specification.MpptCount.HasValue
            && specification.StringInputsPerMppt.HasValue
            && specification.MaxOperatingCurrentPerMpptA.HasValue
            && specification.MaxShortCircuitCurrentPerMpptA.HasValue
            && !string.IsNullOrWhiteSpace(specification.RequiredGridCertifications);
}
