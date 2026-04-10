using System.ComponentModel.DataAnnotations;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ApplicationType
{
    public sealed class ApplicationTypeLanguageUpdateRequest
    {
        [Required]
        public LanguageCode LanguageCode { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; }

        public bool IsActive { get; set; } = true;
    }
}

