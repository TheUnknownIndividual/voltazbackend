using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ProdcutCategory
{
    public sealed record ProductCategoryLanguageDto(
        LanguageCode LanguageCode,
        string CategoryName
        );

}
