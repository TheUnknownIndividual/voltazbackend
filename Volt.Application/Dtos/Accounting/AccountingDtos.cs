using Volt.Application.Dtos.ExecutionProjects;

namespace Volt.Application.Dtos.Accounting;

public sealed class AccountingOverviewDto
{
    public IReadOnlyList<AccountingEmployeeDto> Employees { get; set; } = Array.Empty<AccountingEmployeeDto>();
    public IReadOnlyList<AccountingProjectDto> Projects { get; set; } = Array.Empty<AccountingProjectDto>();
}

/// <summary>
/// Staff directory data used by Accounting and Human Resources. This is deliberately
/// a read model: it contains no password material and no page-management mutation data.
/// </summary>
public sealed record AccountingEmployeeDto(
    int Id,
    string Username,
    string DisplayName,
    bool IsActive,
    decimal? MonthlySalary,
    DateTime? EmploymentStartDate,
    DateTime? SalaryPaymentDate,
    string Role,
    bool IsSuperAdmin,
    bool IsStakeholder,
    bool HasTelegramConnection);
public sealed class UpdateAccountingEmployeeRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public decimal? MonthlySalary { get; set; }
    public DateTime? EmploymentStartDate { get; set; }
    public DateTime? SalaryPaymentDate { get; set; }
    public bool ClearMonthlySalary { get; set; }
}
public sealed record AccountingWorkerDto(int Id, string FirstName, string LastName, decimal AmountPaid, DateTime StartDate, DateTime EndDate, string Note);
public sealed record AccountingStaffDto(int Id, string DisplayName, string RoleName, DateTime? EmploymentStartDate, DateTime? SalaryPaymentDate, decimal? MonthlySalary);
public sealed record AccountingBoqItemDto(int Id, string ItemName, string Unit, decimal PlannedQuantity, decimal? UnitCost, string Note);
public sealed record AccountingExpenseDto(int Id, string Category, string Name, decimal Amount, DateTime ExpenseDate, string ReceiptUrl, string Note, string CreatedByName);
public sealed record AccountingProjectDto(
    int Id,
    string Name,
    string Status,
    bool IsArchived,
    decimal BoqPlannedCost,
    decimal WarehouseCost,
    decimal StaffSalaryCost,
    decimal ExternalWorkerCost,
    decimal MiscellaneousCost,
    decimal TotalCost,
    decimal? SourceOfferPrice,
    string Location,
    string Description,
    string ProjectManagerName,
    DateTime? PlannedStartDate,
    DateTime? PlannedEndDate,
    IReadOnlyList<AccountingStaffDto> Staff,
    IReadOnlyList<AccountingBoqItemDto> BoqItems,
    IReadOnlyList<AccountingWorkerDto> ExternalWorkers,
    IReadOnlyList<AccountingExpenseDto> Expenses);

public sealed class CreateAccountingExpenseRequest
{
    public string Category { get; set; } = "Müxtəlif xərclər";
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string ReceiptUrl { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
