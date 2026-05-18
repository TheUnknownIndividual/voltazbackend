using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.Promotion
{
    public sealed record PromotionDto( IReadOnlyList<PromotionLanguageDto> Languages);
}
