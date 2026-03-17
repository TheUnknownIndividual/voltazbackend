using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.About
{
    public sealed record AboutImageDto(
        int Id,
        string ImagePath,
        bool IsActive
    );
}
