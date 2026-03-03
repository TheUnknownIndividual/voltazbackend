using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Admin
{
    public sealed record AdminDto(int Id, string Username, Role Role, bool IsActive);
}
