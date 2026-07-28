using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.SolarAnalytics
{
    public sealed class AdminSolarDocxExportRequest : AdminSolarExportRequest
    {
        [Required]
        [MaxLength(10)]
        public string DocumentCode { get; set; }

    }
}
