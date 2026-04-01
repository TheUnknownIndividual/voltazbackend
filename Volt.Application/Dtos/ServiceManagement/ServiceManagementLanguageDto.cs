using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ServiceManagement
{
    public record ServiceManagementLanguageDto(int Id,
        LanguageCode LanguageCode,
        string Title,
        string Description,
        bool IsActive);
}
