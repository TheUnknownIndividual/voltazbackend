using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Order
{
    public sealed class OrderStatusUpdateRequest
    {
        [Range(1, 6)]
        public byte Status { get; set; }

        [Range(1, 6)]
        public byte? PaymentStatus { get; set; }
    }
}
