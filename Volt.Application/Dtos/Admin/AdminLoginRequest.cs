using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.Admin
{
    public sealed record AdminLoginRequest(string Username, string Password);
}
