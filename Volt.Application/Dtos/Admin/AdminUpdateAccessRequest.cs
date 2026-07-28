namespace Volt.Application.Dtos.Admin
{
    public sealed record AdminUpdateAccessRequest(
        string DisplayName,
        bool IsActive,
        IReadOnlyList<Volt.Domain.Enums.AdminPage>? AllowedPages,
        bool CanDeleteProjects,
        bool CanEditProjects,
        bool CanApproveWarehouseMovements,
        bool IsStakeholder,
        decimal? MonthlySalary,
        long? TelegramChatId,
        bool ClearSalary = false);
}
