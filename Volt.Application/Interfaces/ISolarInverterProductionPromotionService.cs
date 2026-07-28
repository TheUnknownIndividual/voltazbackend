#nullable enable

using Volt.Domain.Entities;

namespace Volt.Application.Interfaces;

public sealed record SolarInverterProductionPromotionResult(
    bool Enabled,
    bool Promoted,
    string Message,
    DateTime? PromotedAtUtc);

public interface ISolarInverterProductionPromotionService
{
    Task<SolarInverterProductionPromotionResult> PromoteAsync(
        SolarInverterSpecification specification,
        Product sourceProduct,
        SolarInverterDatasheetDocument? document,
        CancellationToken ct = default);
}
