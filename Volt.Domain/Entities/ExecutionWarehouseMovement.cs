namespace Volt.Domain.Entities;

public sealed class ExecutionWarehouseMovement
{
    public int Id { get; set; }
    public int ExecutionProjectId { get; set; }
    public ExecutionProject ExecutionProject { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int? ProductParametrId { get; set; }
    public ProductParametr? ProductParametr { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = "ədəd";
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    /// <summary>OUT = warehouse exit, IN = return/entry.</summary>
    public string Direction { get; set; } = "OUT";
    public DateTime MovedAt { get; set; }
    public int RecordedByAdminUserId { get; set; }
    public AdminUser RecordedBy { get; set; } = null!;
    public string ApprovalStatus { get; set; } = "Pending";
    public int? ApprovedByAdminUserId { get; set; }
    public AdminUser? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string ApprovalNote { get; set; } = string.Empty;
}
