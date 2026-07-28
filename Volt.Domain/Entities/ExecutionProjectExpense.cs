namespace Volt.Domain.Entities;

/// <summary>Any non-payroll project cost, including miscellaneous services, purchases and travel.</summary>
public sealed class ExecutionProjectExpense
{
    public int Id { get; set; }
    public int ExecutionProjectId { get; set; }
    public ExecutionProject ExecutionProject { get; set; } = null!;
    public string Category { get; set; } = "Müxtəlif xərclər";
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string ReceiptUrl { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public int CreatedByAdminUserId { get; set; }
    public AdminUser CreatedByAdminUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
