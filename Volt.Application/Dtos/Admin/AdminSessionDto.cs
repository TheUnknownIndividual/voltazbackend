using Volt.Domain.Enums;

namespace Volt.Application.Dtos.Admin
{
    public sealed record AdminSessionDto(
        int Id,
        string Username,
        string DisplayName,
        bool IsSuperAdmin,
        bool CanDeleteProjects,
        bool CanEditProjects,
        bool CanApproveWarehouseMovements,
        bool CanViewAccounting,
        IReadOnlyList<AdminPage> AllowedPages);
}
