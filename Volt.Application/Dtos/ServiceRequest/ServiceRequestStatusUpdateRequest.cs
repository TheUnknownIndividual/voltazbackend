using System.ComponentModel.DataAnnotations;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ServiceRequest
{
    public sealed class ServiceRequestStatusUpdateRequest
    {
        [Required]
        public byte Status { get; set; }
    }
}
