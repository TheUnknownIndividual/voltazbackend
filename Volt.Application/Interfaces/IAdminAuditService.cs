namespace Volt.Application.Interfaces
{
    public interface IAdminAuditService
    {
        Task WriteAsync(int? adminUserId, string? actorUsername, string action, string? targetType, string? targetId, string? summary, bool succeeded, CancellationToken ct = default);
    }
}
