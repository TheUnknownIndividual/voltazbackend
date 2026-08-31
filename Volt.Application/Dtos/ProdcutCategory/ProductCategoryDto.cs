using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.ProdcutCategory
{
    public sealed record ProductCategoryDto(
        int Id,
        string SeoKey,
        IReadOnlyList<ProductCategoryLanguageDto> Languages,
        bool ShowOnHomePage,
        int HomePageDisplayOrder,
        int? HomePageProductId
        );
}
