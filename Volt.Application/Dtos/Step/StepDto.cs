using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.Step
{
    public sealed record StepDto(
        int Id,
        string ImagePath,
        int Position,
        bool IsActive,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IReadOnlyList<StepLanguageDto> Languages
    );
}
