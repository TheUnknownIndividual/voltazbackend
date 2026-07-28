using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos.Admin
{
    public sealed record AdminCreateRequest(
        string Username,
        string Password,
        string DisplayName,
        IReadOnlyList<Volt.Domain.Enums.AdminPage>? AllowedPages,
        bool CanDeleteProjects,
        bool CanEditProjects,
        bool CanApproveWarehouseMovements,
        bool IsStakeholder,
        decimal? MonthlySalary,
        long? TelegramChatId);
}
