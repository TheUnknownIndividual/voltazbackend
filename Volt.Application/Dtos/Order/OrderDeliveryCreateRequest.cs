using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Order
{
    public sealed class OrderDeliveryCreateRequest
    {
        [Range(1, 3)]
        public byte Method { get; set; }

        [MaxLength(120)]
        public string CityOrRegion { get; set; }

        [MaxLength(120)]
        public string District { get; set; }

        [MaxLength(240)]
        public string StreetAndBuilding { get; set; }

        [MaxLength(120)]
        public string ApartmentOrOffice { get; set; }

        [MaxLength(800)]
        public string DeliveryNotes { get; set; }

        [MaxLength(160)]
        public string PickupLocation { get; set; }
    }
}
