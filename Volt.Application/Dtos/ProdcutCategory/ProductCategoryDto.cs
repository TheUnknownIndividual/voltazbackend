using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.ProdcutCategory
{
    public sealed record ProductCategoryDto(
        int Id,
        IReadOnlyList<ProductCategoryLanguageDto> Languages
        );
}
