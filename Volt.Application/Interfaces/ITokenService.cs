using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Application.Dtos;
using Volt.Domain.Entities;

namespace Volt.Application.Interfaces
{
    public interface ITokenService
    {
        TokenDto CreateAdminAccessToken(AdminUser admin);
    }
}
