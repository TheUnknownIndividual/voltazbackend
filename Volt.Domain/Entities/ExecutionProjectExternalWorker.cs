namespace Volt.Domain.Entities;

/// <summary>Short-term, off-payroll project helper. Kept separate from AdminUsers.</summary>
public sealed class ExecutionProjectExternalWorker
{
    public int Id { get; set; }
    public int ExecutionProjectId { get; set; }
    public ExecutionProject ExecutionProject { get; set; } = null!;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public decimal AmountPaid { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Note { get; set; } = string.Empty;
    public int CreatedByAdminUserId { get; set; }
    public AdminUser CreatedByAdminUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
