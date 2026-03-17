using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.About
{
    public sealed record  AboutLanguageDto(
        int Id,
        LanguageCode LanguageCode,
        string Title,
        string Description,
        bool IsActive
        );
}
