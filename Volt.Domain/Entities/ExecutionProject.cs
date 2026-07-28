namespace Volt.Domain.Entities;

/// <summary>Internal delivery record for an accepted public or tracked project.</summary>
public sealed class ExecutionProject
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? AdminTrackedProjectId { get; set; }
    public AdminTrackedProject? AdminTrackedProject { get; set; }
    public int ProjectManagerAdminUserId { get; set; }
    public AdminUser ProjectManager { get; set; } = null!;
    public string Status { get; set; } = "Aktiv";
    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string ArchiveReason { get; set; } = string.Empty;
    public ICollection<ExecutionProjectStaff> Staff { get; set; } = new List<ExecutionProjectStaff>();
    public ICollection<ExecutionProjectExternalWorker> ExternalWorkers { get; set; } = new List<ExecutionProjectExternalWorker>();
    public ICollection<ExecutionProjectExpense> Expenses { get; set; } = new List<ExecutionProjectExpense>();
    public ICollection<ExecutionProjectBoqItem> BoqItems { get; set; } = new List<ExecutionProjectBoqItem>();
    public ICollection<ExecutionWarehouseMovement> WarehouseMovements { get; set; } = new List<ExecutionWarehouseMovement>();
    public ICollection<ExecutionProjectTask> Tasks { get; set; } = new List<ExecutionProjectTask>();
}
