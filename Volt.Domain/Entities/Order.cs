using Volt.Domain.Enums;

namespace Volt.Domain.Entities
{
    public class Order
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; }
        public byte Status { get; set; } = (byte)OrderStatus.New;
        public byte PaymentStatus { get; set; } = (byte)OrderPaymentStatus.Pending;
        public byte PaymentMethod { get; set; } = (byte)OrderPaymentMethod.PaymentAfterConfirmation;
        public byte Source { get; set; } = (byte)OrderSource.Cart;
        public byte Intent { get; set; } = (byte)OrderIntent.Purchase;
        public bool RequiresManualConfirmation { get; set; }
        public bool IsViewedByAdmin { get; set; }
        public DateTime? AdminViewedAt { get; set; }
        public bool AcceptedTerms { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public byte DeliveryMethod { get; set; } = (byte)OrderDeliveryMethod.ConfirmByPhone;
        public string CityOrRegion { get; set; }
        public string District { get; set; }
        public string StreetAndBuilding { get; set; }
        public string ApartmentOrOffice { get; set; }
        public string DeliveryNotes { get; set; }
        public string PickupLocation { get; set; }
        public decimal ProductsSubtotal { get; set; }
        public decimal? DeliveryFee { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal FinalTotal { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}
