using Volt.Application.Interfaces;
using Volt.Domain.Entities;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services
{
    public sealed class AdminAuditService : IAdminAuditService
    {
        private readonly DataContext _context;

        public AdminAuditService(DataContext context) => _context = context;

        public async Task WriteAsync(int? adminUserId, string? actorUsername, string action, string? targetType, string? targetId, string? summary, bool succeeded, CancellationToken ct = default)
        {
            _context.AdminAuditLogs.Add(new AdminAuditLog
            {
                AdminUserId = adminUserId,
                ActorUsername = (actorUsername ?? string.Empty).Trim().ToLowerInvariant(),
                Action = action,
                TargetType = targetType,
                TargetId = targetId,
                Summary = summary,
                Succeeded = succeeded,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(ct);
        }
    }
}
