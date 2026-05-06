using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Step;

namespace Volt.Application.Dtos.ServiceManagement
{
    public record ServiceManagementDto(int Id,
        bool IsActive,
        string Icon,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<ServiceManagementLanguageDto> Languages);
}
