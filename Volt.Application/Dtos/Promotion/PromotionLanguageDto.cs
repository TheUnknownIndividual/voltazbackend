using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Promotion
{
    public sealed record PromotionLanguageDto(
        LanguageCode LanguageCode,
        string PromotionName);
}
