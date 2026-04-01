using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Step;

namespace Volt.Application.Dtos.ServiceManagement
{
    public record ServiceManagementDto(int Id,
        string ImagePath,
        int Position,
        bool ActiveStatus,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<ServiceManagementLanguageDto> Languages);
}
