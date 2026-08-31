using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Step;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ServiceManagement
{
    public record ServiceManagementDto(int Id,
        bool IsActive,
        string Icon,
        ServiceCategory Category,
        string ReadMoreUrl,
        string DetailPageSlug,
        string BannerImageUrl,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<ServiceManagementLanguageDto> Languages);
}
