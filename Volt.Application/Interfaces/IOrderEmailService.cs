using Volt.Application.Dtos.Order;

namespace Volt.Application.Interfaces
{
    public interface IOrderEmailService
    {
        Task SendOrderConfirmationAsync(OrderDto order, CancellationToken ct = default);
    }
}
