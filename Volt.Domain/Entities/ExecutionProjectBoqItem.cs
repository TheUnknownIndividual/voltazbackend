namespace Volt.Domain.Entities;

public sealed class ExecutionProjectBoqItem
{
    public int Id { get; set; }
    public int ExecutionProjectId { get; set; }
    public ExecutionProject ExecutionProject { get; set; } = null!;
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = "ədəd";
    public decimal PlannedQuantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
