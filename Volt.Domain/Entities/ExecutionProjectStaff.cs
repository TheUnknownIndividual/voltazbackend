namespace Volt.Domain.Entities;

public sealed class ExecutionProjectStaff
{
    public int Id { get; set; }
    public int ExecutionProjectId { get; set; }
    public ExecutionProject ExecutionProject { get; set; } = null!;
    public int AdminUserId { get; set; }
    public AdminUser AdminUser { get; set; } = null!;
    public string RoleName { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime AddedAt { get; set; }
}
