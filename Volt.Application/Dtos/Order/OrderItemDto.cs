namespace Volt.Application.Dtos.Order
{
    public sealed record OrderItemDto(
        int Id,
        int ProductId,
        int? ProductParametrId,
        string ProductName,
        string ProductImageUrl,
        string SelectedPower,
        int Quantity,
        int ReservedQuantity,
        bool InventoryReleased,
        decimal UnitPrice,
        decimal LineTotal);
}
