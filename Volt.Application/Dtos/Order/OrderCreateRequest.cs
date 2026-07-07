using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Order
{
    public sealed class OrderCreateRequest
    {
        [Required]
        public OrderContactCreateRequest Contact { get; set; }

        [Required]
        public OrderDeliveryCreateRequest Delivery { get; set; }

        [Range(1, 4)]
        public byte PaymentMethod { get; set; }

        [Range(1, 2)]
        public byte Source { get; set; } = 1;

        [Range(1, 3)]
        public byte Intent { get; set; } = 1;

        public bool AcceptedTerms { get; set; }

        [Required]
        [MinLength(1)]
        public List<OrderItemCreateRequest> Items { get; set; } = new();
    }
}
