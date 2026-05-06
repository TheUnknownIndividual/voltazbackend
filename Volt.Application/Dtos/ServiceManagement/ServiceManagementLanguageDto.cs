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
        string Content1,
        string Content2,
        string Content3,
        string Content4,
        bool IsActive);
}
