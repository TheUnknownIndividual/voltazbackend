#nullable enable

using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services;

public sealed class SolarInverterProductionPromotionService :
    ISolarInverterProductionPromotionService
{
    private readonly DataContext _sourceContext;
    private readonly SolarInverterPromotionOptions _options;

    public SolarInverterProductionPromotionService(
        DataContext sourceContext,
        IOptions<SolarInverterPromotionOptions> options)
    {
        _sourceContext = sourceContext;
        _options = options.Value;
    }

    public async Task<SolarInverterProductionPromotionResult> PromoteAsync(
        SolarInverterSpecification specification,
        Product sourceProduct,
        SolarInverterDatasheetDocument? document,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return new SolarInverterProductionPromotionResult(
                false,
                false,
                "Automatic production promotion is disabled. The confirmed dataset is ready for manual promotion.",
                null);
        }

        if (string.IsNullOrWhiteSpace(_options.ProductionConnectionString))
        {
            return new SolarInverterProductionPromotionResult(
                true,
                false,
                "Automatic promotion is enabled, but no production connection is provisioned.",
                null);
        }

        if (document is null)
        {
            return new SolarInverterProductionPromotionResult(
                true,
                false,
                "The immutable source document could not be resolved, so production was not changed.",
                null);
        }

        SqlConnectionStringBuilder sourceConnection;
        SqlConnectionStringBuilder productionConnection;
        try
        {
            sourceConnection = new SqlConnectionStringBuilder(
                _sourceContext.Database.GetDbConnection().ConnectionString);
            productionConnection = new SqlConnectionStringBuilder(
                _options.ProductionConnectionString);
        }
        catch (ArgumentException)
        {
            return new SolarInverterProductionPromotionResult(
                true,
                false,
                "The promotion database configuration is invalid.",
                null);
        }

        if (!sourceConnection.InitialCatalog.Contains("test", StringComparison.OrdinalIgnoreCase) ||
            !productionConnection.InitialCatalog.Contains("prod", StringComparison.OrdinalIgnoreCase) ||
            sourceConnection.InitialCatalog.Equals(
                productionConnection.InitialCatalog,
                StringComparison.OrdinalIgnoreCase))
        {
            return new SolarInverterProductionPromotionResult(
                true,
                false,
                "Promotion safety validation failed: source must be Test and target must be Prod.",
                null);
        }

        try
        {
            await using var connection = new SqlConnection(productionConnection.ConnectionString);
            await connection.OpenAsync(ct);
            if (!await HasRequiredSchemaAsync(connection, ct))
            {
                return new SolarInverterProductionPromotionResult(
                    true,
                    false,
                    "Production is missing the datasheet engineering/document migrations. No data was written.",
                    null);
            }

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);
            var productIds = await FindProductionProductIdsAsync(
                connection,
                transaction,
                sourceProduct.ProductName,
                ct);
            if (productIds.Count != 1)
            {
                await transaction.RollbackAsync(ct);
                return new SolarInverterProductionPromotionResult(
                    true,
                    false,
                    productIds.Count == 0
                        ? "No active production product has the same product name. No data was written."
                        : "Multiple active production products have the same product name. No data was written.",
                    null);
            }

            var productionProductId = productIds[0];
            var productionTechnicalPower = await FindProductionTechnicalPowerAsync(
                connection,
                transaction,
                productionProductId,
                specification.TechnicalPower,
                ct);
            if (productionTechnicalPower is null)
            {
                await transaction.RollbackAsync(ct);
                return new SolarInverterProductionPromotionResult(
                    true,
                    false,
                    "The matching wattage variant does not exist on the production product. No data was written.",
                    null);
            }

            await UpsertDocumentAsync(
                connection,
                transaction,
                document,
                specification.CorrectedExtractedText,
                ct);
            await UpsertSpecificationAsync(
                connection,
                transaction,
                productionProductId,
                productionTechnicalPower,
                specification,
                ct);
            await transaction.CommitAsync(ct);
            var promotedAtUtc = DateTime.UtcNow;
            return new SolarInverterProductionPromotionResult(
                true,
                true,
                "The confirmed inverter dataset and source document were promoted to production.",
                promotedAtUtc);
        }
        catch (SqlException error)
        {
            return new SolarInverterProductionPromotionResult(
                true,
                false,
                $"Production rejected the promotion operation (SQL {error.Number}). No completed promotion was recorded.",
                null);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new SolarInverterProductionPromotionResult(
                true,
                false,
                $"Production promotion failed ({error.GetType().Name}). No completed promotion was recorded.",
                null);
        }
    }

    private static async Task<bool> HasRequiredSchemaAsync(
        SqlConnection connection,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CASE WHEN
                OBJECT_ID(N'SolarInverterSpecifications', N'U') IS NOT NULL
                AND OBJECT_ID(N'SolarInverterDatasheetDocuments', N'U') IS NOT NULL
                AND COL_LENGTH(N'SolarInverterSpecifications', N'DatasheetContentJson') IS NOT NULL
                AND COL_LENGTH(N'SolarInverterSpecifications', N'DatasheetReviewedAt') IS NOT NULL
            THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
            """;
        return (bool)(await command.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<List<int>> FindProductionProductIdsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string productName,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT Id
            FROM Products WITH (UPDLOCK, HOLDLOCK)
            WHERE IsActive = 1 AND ProductName = @ProductName
            """;
        command.Parameters.Add("@ProductName", SqlDbType.NVarChar, 500).Value = productName;
        var result = new List<int>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetInt32(0));
        }

        return result;
    }

    private static async Task<string?> FindProductionTechnicalPowerAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int productId,
        string sourceTechnicalPower,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT TechnicalPower
            FROM ProductParametrs WITH (UPDLOCK, HOLDLOCK)
            WHERE ProductId = @ProductId AND IsActive = 1
            """;
        command.Parameters.Add("@ProductId", SqlDbType.Int).Value = productId;
        var normalizedSourcePower = NormalizeTechnicalPower(sourceTechnicalPower);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var candidate = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (candidate is not null &&
                NormalizeTechnicalPower(candidate).Equals(
                    normalizedSourcePower,
                    StringComparison.Ordinal))
            {
                return candidate.Trim();
            }
        }

        return null;
    }

    private static async Task UpsertDocumentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SolarInverterDatasheetDocument document,
        string? correctedExtractedText,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            IF EXISTS (
                SELECT 1
                FROM SolarInverterDatasheetDocuments WITH (UPDLOCK, HOLDLOCK)
                WHERE Sha256 = @Sha256
            )
            BEGIN
                UPDATE SolarInverterDatasheetDocuments
                SET SourceUrl = @SourceUrl,
                    ParserVersion = @ParserVersion,
                    DocumentKind = @DocumentKind,
                    PageCount = @PageCount,
                    RequiresOcr = @RequiresOcr,
                    ExtractedText = @ExtractedText,
                    ParsedContentJson = @ParsedContentJson,
                    ImportedAtUtc = @ImportedAtUtc
                WHERE Sha256 = @Sha256;
            END
            ELSE
            BEGIN
                INSERT INTO SolarInverterDatasheetDocuments
                (
                    SourceUrl, Sha256, ParserVersion, DocumentKind, PageCount,
                    RequiresOcr, ExtractedText, ParsedContentJson, ImportedAtUtc
                )
                VALUES
                (
                    @SourceUrl, @Sha256, @ParserVersion, @DocumentKind, @PageCount,
                    @RequiresOcr, @ExtractedText, @ParsedContentJson, @ImportedAtUtc
                );
            END
            """;
        command.Parameters.Add("@SourceUrl", SqlDbType.NVarChar, 1000).Value = document.SourceUrl;
        command.Parameters.Add("@Sha256", SqlDbType.VarChar, 64).Value = document.Sha256;
        command.Parameters.Add("@ParserVersion", SqlDbType.NVarChar, 100).Value = document.ParserVersion;
        command.Parameters.Add("@DocumentKind", SqlDbType.NVarChar, 60).Value = document.DocumentKind;
        command.Parameters.Add("@PageCount", SqlDbType.Int).Value = document.PageCount;
        command.Parameters.Add("@RequiresOcr", SqlDbType.Bit).Value = document.RequiresOcr;
        command.Parameters.Add("@ExtractedText", SqlDbType.NVarChar, -1).Value =
            string.IsNullOrWhiteSpace(correctedExtractedText)
                ? document.ExtractedText
                : correctedExtractedText.Trim();
        command.Parameters.Add("@ParsedContentJson", SqlDbType.NVarChar, -1).Value =
            document.ParsedContentJson;
        command.Parameters.Add("@ImportedAtUtc", SqlDbType.DateTime2).Value = DateTime.UtcNow;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpsertSpecificationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int productId,
        string technicalPower,
        SolarInverterSpecification x,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            IF EXISTS (
                SELECT 1 FROM SolarInverterSpecifications WITH (UPDLOCK, HOLDLOCK)
                WHERE ProductId = @ProductId AND TechnicalPower = @TechnicalPower
            )
            BEGIN
                UPDATE SolarInverterSpecifications SET
                    ModelLabel = @ModelLabel,
                    SystemType = @SystemType,
                    Phase = @Phase,
                    NominalAcKw = @NominalAcKw,
                    MaxDcKw = @MaxDcKw,
                    MpptCount = @MpptCount,
                    InputCount = @InputCount,
                    MpptRange = @MpptRange,
                    MaxDcVoltage = @MaxDcVoltage,
                    MaxInputCurrent = @MaxInputCurrent,
                    Manufacturer = @Manufacturer,
                    RegionalGridVersion = @RegionalGridVersion,
                    DatasheetUrl = @DatasheetUrl,
                    DatasheetRevision = @DatasheetRevision,
                    DatasheetContentJson = @DatasheetContentJson,
                    DatasheetReviewedAt = @DatasheetReviewedAt,
                    MaxAcApparentPowerKva = @MaxAcApparentPowerKva,
                    MaxAcOutputCurrentA = @MaxAcOutputCurrentA,
                    NominalAcVoltageV = @NominalAcVoltageV,
                    SupportedGridVoltageRange = @SupportedGridVoltageRange,
                    SupportedFrequencyRange = @SupportedFrequencyRange,
                    StartVoltageV = @StartVoltageV,
                    MpptMinVoltageV = @MpptMinVoltageV,
                    MpptMaxVoltageV = @MpptMaxVoltageV,
                    NominalDcVoltageV = @NominalDcVoltageV,
                    StringInputsPerMppt = @StringInputsPerMppt,
                    MaxOperatingCurrentPerStringA = @MaxOperatingCurrentPerStringA,
                    MaxOperatingCurrentPerMpptA = @MaxOperatingCurrentPerMpptA,
                    MaxShortCircuitCurrentPerStringA = @MaxShortCircuitCurrentPerStringA,
                    MaxShortCircuitCurrentPerMpptA = @MaxShortCircuitCurrentPerMpptA,
                    HasIntegratedDcSwitch = @HasIntegratedDcSwitch,
                    AcSpdClass = @AcSpdClass,
                    DcSpdClass = @DcSpdClass,
                    HasAfci = @HasAfci,
                    RequiredGridCertifications = @RequiredGridCertifications,
                    WarrantyYears = @WarrantyYears,
                    IsEligible = @IsEligible
                WHERE ProductId = @ProductId AND TechnicalPower = @TechnicalPower;
            END
            ELSE
            BEGIN
                INSERT INTO SolarInverterSpecifications
                (
                    ProductId, TechnicalPower, ModelLabel, SystemType, Phase,
                    NominalAcKw, MaxDcKw, MpptCount, InputCount, MpptRange,
                    MaxDcVoltage, MaxInputCurrent, Manufacturer, RegionalGridVersion,
                    DatasheetUrl, DatasheetRevision, DatasheetContentJson,
                    DatasheetReviewedAt, MaxAcApparentPowerKva, MaxAcOutputCurrentA,
                    NominalAcVoltageV, SupportedGridVoltageRange,
                    SupportedFrequencyRange, StartVoltageV, MpptMinVoltageV,
                    MpptMaxVoltageV, NominalDcVoltageV, StringInputsPerMppt,
                    MaxOperatingCurrentPerStringA, MaxOperatingCurrentPerMpptA,
                    MaxShortCircuitCurrentPerStringA, MaxShortCircuitCurrentPerMpptA,
                    HasIntegratedDcSwitch, AcSpdClass, DcSpdClass, HasAfci,
                    RequiredGridCertifications, WarrantyYears, IsEligible
                )
                VALUES
                (
                    @ProductId, @TechnicalPower, @ModelLabel, @SystemType, @Phase,
                    @NominalAcKw, @MaxDcKw, @MpptCount, @InputCount, @MpptRange,
                    @MaxDcVoltage, @MaxInputCurrent, @Manufacturer, @RegionalGridVersion,
                    @DatasheetUrl, @DatasheetRevision, @DatasheetContentJson,
                    @DatasheetReviewedAt, @MaxAcApparentPowerKva, @MaxAcOutputCurrentA,
                    @NominalAcVoltageV, @SupportedGridVoltageRange,
                    @SupportedFrequencyRange, @StartVoltageV, @MpptMinVoltageV,
                    @MpptMaxVoltageV, @NominalDcVoltageV, @StringInputsPerMppt,
                    @MaxOperatingCurrentPerStringA, @MaxOperatingCurrentPerMpptA,
                    @MaxShortCircuitCurrentPerStringA, @MaxShortCircuitCurrentPerMpptA,
                    @HasIntegratedDcSwitch, @AcSpdClass, @DcSpdClass, @HasAfci,
                    @RequiredGridCertifications, @WarrantyYears, @IsEligible
                );
            END
            """;
        Add(command, "@ProductId", SqlDbType.Int, productId);
        Add(command, "@TechnicalPower", SqlDbType.NVarChar, technicalPower, 64);
        Add(command, "@ModelLabel", SqlDbType.NVarChar, x.ModelLabel, 240);
        Add(command, "@SystemType", SqlDbType.NVarChar, x.SystemType, 32);
        Add(command, "@Phase", SqlDbType.NVarChar, x.Phase, 16);
        AddDecimal(command, "@NominalAcKw", x.NominalAcKw);
        AddDecimal(command, "@MaxDcKw", x.MaxDcKw);
        Add(command, "@MpptCount", SqlDbType.Int, x.MpptCount);
        Add(command, "@InputCount", SqlDbType.Int, x.InputCount);
        Add(command, "@MpptRange", SqlDbType.NVarChar, x.MpptRange, 64);
        Add(command, "@MaxDcVoltage", SqlDbType.Int, x.MaxDcVoltage);
        Add(command, "@MaxInputCurrent", SqlDbType.NVarChar, x.MaxInputCurrent, 64);
        Add(command, "@Manufacturer", SqlDbType.NVarChar, x.Manufacturer, 120);
        Add(command, "@RegionalGridVersion", SqlDbType.NVarChar, x.RegionalGridVersion, 120);
        Add(command, "@DatasheetUrl", SqlDbType.NVarChar, x.DatasheetUrl, 1000);
        Add(command, "@DatasheetRevision", SqlDbType.NVarChar, x.DatasheetRevision, 120);
        Add(command, "@DatasheetContentJson", SqlDbType.NVarChar, x.DatasheetContentJson, -1);
        Add(command, "@DatasheetReviewedAt", SqlDbType.DateTime2, x.DatasheetReviewedAt);
        AddDecimal(command, "@MaxAcApparentPowerKva", x.MaxAcApparentPowerKva);
        AddDecimal(command, "@MaxAcOutputCurrentA", x.MaxAcOutputCurrentA);
        AddDecimal(command, "@NominalAcVoltageV", x.NominalAcVoltageV);
        Add(command, "@SupportedGridVoltageRange", SqlDbType.NVarChar, x.SupportedGridVoltageRange, 120);
        Add(command, "@SupportedFrequencyRange", SqlDbType.NVarChar, x.SupportedFrequencyRange, 80);
        Add(command, "@StartVoltageV", SqlDbType.Int, x.StartVoltageV);
        Add(command, "@MpptMinVoltageV", SqlDbType.Int, x.MpptMinVoltageV);
        Add(command, "@MpptMaxVoltageV", SqlDbType.Int, x.MpptMaxVoltageV);
        Add(command, "@NominalDcVoltageV", SqlDbType.Int, x.NominalDcVoltageV);
        Add(command, "@StringInputsPerMppt", SqlDbType.Int, x.StringInputsPerMppt);
        AddDecimal(command, "@MaxOperatingCurrentPerStringA", x.MaxOperatingCurrentPerStringA);
        AddDecimal(command, "@MaxOperatingCurrentPerMpptA", x.MaxOperatingCurrentPerMpptA);
        AddDecimal(command, "@MaxShortCircuitCurrentPerStringA", x.MaxShortCircuitCurrentPerStringA);
        AddDecimal(command, "@MaxShortCircuitCurrentPerMpptA", x.MaxShortCircuitCurrentPerMpptA);
        Add(command, "@HasIntegratedDcSwitch", SqlDbType.Bit, x.HasIntegratedDcSwitch);
        Add(command, "@AcSpdClass", SqlDbType.NVarChar, x.AcSpdClass, 80);
        Add(command, "@DcSpdClass", SqlDbType.NVarChar, x.DcSpdClass, 80);
        Add(command, "@HasAfci", SqlDbType.Bit, x.HasAfci);
        Add(command, "@RequiredGridCertifications", SqlDbType.NVarChar, x.RequiredGridCertifications, 1000);
        Add(command, "@WarrantyYears", SqlDbType.Int, x.WarrantyYears);
        Add(command, "@IsEligible", SqlDbType.Bit, x.IsEligible);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void Add(
        SqlCommand command,
        string name,
        SqlDbType type,
        object? value,
        int? size = null)
    {
        var parameter = size.HasValue
            ? command.Parameters.Add(name, type, size.Value)
            : command.Parameters.Add(name, type);
        parameter.Value = value ?? DBNull.Value;
    }

    private static void AddDecimal(
        SqlCommand command,
        string name,
        decimal? value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value = value ?? (object)DBNull.Value;
    }

    private static string NormalizeTechnicalPower(string value)
        => string.Concat(value.Where(character => !char.IsWhiteSpace(character)))
            .ToLowerInvariant();
}
