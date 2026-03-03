using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos.Admin;
using Volt.Application.Dtos;

namespace Volt.Application.Interfaces
{
    public interface IAdminAuthService
    {
        Task<ApiResponse<TokenDto>> LoginAsync(AdminLoginRequest request, CancellationToken ct = default);
    }
}
