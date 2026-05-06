using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.About
{
    public sealed record AboutDto(
        int Id,
        string ImagePath,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<AboutLanguageDto> Languages
    );
}
