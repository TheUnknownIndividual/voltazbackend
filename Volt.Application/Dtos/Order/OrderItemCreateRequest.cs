using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Order
{
    public sealed class OrderItemCreateRequest
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [MaxLength(120)]
        public string SelectedPower { get; set; }

        [Range(1, 10000)]
        public int Quantity { get; set; }
    }
}
