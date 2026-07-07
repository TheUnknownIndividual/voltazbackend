using Volt.Application.Dtos;
using Volt.Application.Dtos.Order;

namespace Volt.Application.Interfaces
{
    public interface IOrderService
    {
        Task<ApiResponse<IReadOnlyList<OrderDto>>> GetAllAsync(byte? status = null, CancellationToken ct = default);
        Task<ApiResponse<IReadOnlyList<OrderDto>>> GetByCustomerEmailAsync(string email, CancellationToken ct = default);
        Task<ApiResponse<OrderDto>> GetByIdAsync(int id, CancellationToken ct = default);
        Task<ApiResponse<OrderDto>> LookupAsync(string orderNumber, string email, CancellationToken ct = default);
        Task<ApiResponse<OrderDto>> CreateAsync(OrderCreateRequest request, CancellationToken ct = default);
        Task<ApiResponse<OrderDto>> UpdateStatusAsync(int id, OrderStatusUpdateRequest request, CancellationToken ct = default);
        Task<ApiResponse<OrderDto>> MarkViewedAsync(int id, CancellationToken ct = default);
    }
}
