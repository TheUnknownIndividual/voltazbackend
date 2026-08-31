using Volt.Application.Dtos;
using Volt.Application.Dtos.Accounting;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Interfaces;

namespace Volt.Application.Services;

public sealed class AccountingService : IAccountingService
{
    private readonly IUnitOfWork _uow;
    private readonly IFileService _files;

    public AccountingService(IUnitOfWork uow, IFileService files)
    {
        _uow = uow;
        _files = files;
    }

    public async Task<ApiResponse<AccountingOverviewDto>> GetOverviewAsync(CancellationToken ct = default)
    {
        var employees = await _uow.Repository<AdminUser>().ListNoTrackingAsync(ct);
        // Accounting is also the historical ledger, so archived execution projects remain visible.
        var projects = await _uow.Repository<ExecutionProject>().ListNoTrackingAsync(ct);
        var projectResults = new List<AccountingProjectDto>(projects.Count);
        foreach (var project in projects.OrderByDescending(x => x.CreatedAt))
            projectResults.Add(await MapProjectAsync(project, employees, ct));
        var result = new AccountingOverviewDto
        {
            Employees = employees.OrderBy(x => x.DisplayName ?? x.Username)
                .Select(x => new AccountingEmployeeDto(
                    x.Id,
                    x.Username,
                    DisplayName(x),
                    x.IsActive,
                    x.MonthlySalary,
                    x.EmploymentStartDate,
                    x.SalaryPaymentDate,
                    x.Role.ToString(),
                    x.IsEffectiveSuperAdmin,
                    x.IsStakeholder,
                    x.TelegramChatId.HasValue)).ToList(),
            Projects = projectResults
        };
        return ApiResponse<AccountingOverviewDto>.SuccessResponse(result);
    }

    public async Task<ApiResponse<AccountingOverviewDto>> UpdateEmployeeAsync(int employeeId, UpdateAccountingEmployeeRequest request, CancellationToken ct = default)
    {
        var employee = await _uow.Repository<AdminUser>().FirstOrDefaultAsync(x => x.Id == employeeId, ct);
        if (employee is null)
            return ApiResponse<AccountingOverviewDto>.ErrorResponse(ErrorCode.ADMIN_NOT_FOUND, "Employee was not found.");
        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return ApiResponse<AccountingOverviewDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Employee display name is required.");
        if (request.MonthlySalary is < 0)
            return ApiResponse<AccountingOverviewDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Monthly salary cannot be negative.");

        employee.DisplayName = Normalize(request.DisplayName, 160);
        employee.MonthlySalary = request.ClearMonthlySalary ? null : request.MonthlySalary is > 0 ? request.MonthlySalary : null;
        employee.EmploymentStartDate = request.EmploymentStartDate?.Date;
        employee.SalaryPaymentDate = request.SalaryPaymentDate?.Date;
        _uow.Repository<AdminUser>().Update(employee);
        await _uow.SaveChangesAsync(ct);
        return await GetOverviewAsync(ct);
    }

    public async Task<ApiResponse<AccountingOverviewDto>> AddExpenseAsync(int projectId, CreateAccountingExpenseRequest request, int actorAdminUserId, CancellationToken ct = default)
    {
        if (!await _uow.Repository<ExecutionProject>().AnyAsync(x => x.Id == projectId && !x.ArchivedAt.HasValue, ct))
            return ApiResponse<AccountingOverviewDto>.ErrorResponse(ErrorCode.PROJECT_NOT_FOUND, "Execution project was not found.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Amount <= 0)
            return ApiResponse<AccountingOverviewDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Expense name and a positive amount are required.");
        if ((request.ReceiptUrl ?? string.Empty).Length > 500)
            return ApiResponse<AccountingOverviewDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Receipt URL is too long.");
        await _uow.Repository<ExecutionProjectExpense>().AddAsync(new ExecutionProjectExpense
        {
            ExecutionProjectId = projectId,
            Category = Normalize(request.Category, 80, "Müxtəlif xərclər"),
            Name = Normalize(request.Name, 200),
            Amount = request.Amount,
            ExpenseDate = (request.ExpenseDate == default ? DateTime.UtcNow : request.ExpenseDate).ToUniversalTime(),
            ReceiptUrl = Normalize(request.ReceiptUrl, 500),
            Note = Normalize(request.Note, 1000),
            CreatedByAdminUserId = actorAdminUserId,
            CreatedAt = DateTime.UtcNow
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return await GetOverviewAsync(ct);
    }

    public async Task<ApiResponse<AccountingOverviewDto>> DeleteExpenseAsync(int projectId, int expenseId, CancellationToken ct = default)
    {
        var expense = await _uow.Repository<ExecutionProjectExpense>().FirstOrDefaultAsync(x => x.Id == expenseId && x.ExecutionProjectId == projectId, ct);
        if (expense is null) return ApiResponse<AccountingOverviewDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Expense was not found.");
        _uow.Repository<ExecutionProjectExpense>().Remove(expense);
        await _uow.SaveChangesAsync(ct);
        return await GetOverviewAsync(ct);
    }

    public async Task<ApiResponse<string>> UploadReceiptAsync(int projectId, FileUploadRequest file, CancellationToken ct = default)
    {
        if (!await _uow.Repository<ExecutionProject>().AnyAsync(x => x.Id == projectId && !x.ArchivedAt.HasValue, ct))
            return ApiResponse<string>.ErrorResponse(ErrorCode.PROJECT_NOT_FOUND, "Execution project was not found.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not ".jpg" and not ".jpeg" and not ".png")
            return ApiResponse<string>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Receipt must be a JPG or PNG image.");
        return ApiResponse<string>.SuccessResponse(await _files.UploadImageAsync(file, $"project-expenses/{projectId}", ct));
    }

    private async Task<AccountingProjectDto> MapProjectAsync(ExecutionProject project, IReadOnlyList<AdminUser> employees, CancellationToken ct)
    {
        var source = await ResolveProjectDetailsAsync(project, ct);
        var boq = await _uow.Repository<ExecutionProjectBoqItem>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var movements = await _uow.Repository<ExecutionWarehouseMovement>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id && x.ApprovalStatus == "Approved", ct);
        var staff = await _uow.Repository<ExecutionProjectStaff>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var workers = await _uow.Repository<ExecutionProjectExternalWorker>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var expenses = await _uow.Repository<ExecutionProjectExpense>().ListNoTrackingAsync(x => x.ExecutionProjectId == project.Id, ct);
        var employeeMap = employees.ToDictionary(x => x.Id);
        var staffSalary = staff.Sum(x =>
        {
            var employee = employeeMap.GetValueOrDefault(x.AdminUserId);
            var employmentStart = employee?.EmploymentStartDate;
            var projectStart = project.PlannedStartDate;
            var allocatedStart = employmentStart.HasValue && projectStart.HasValue
                ? (employmentStart.Value.Date > projectStart.Value.Date ? employmentStart : projectStart)
                : employmentStart ?? projectStart;
            return AllocatedSalary(
                employee?.MonthlySalary,
                allocatedStart,
                project.PlannedEndDate);
        });
        var boqCost = boq.Sum(x => x.PlannedQuantity * (x.UnitCost ?? 0));
        var warehouseCost = movements.Sum(x => (x.Direction == "IN" ? -1 : 1) * x.Quantity * (x.UnitCost ?? 0));
        var workerCost = workers.Sum(x => x.AmountPaid);
        var expenseCost = expenses.Sum(x => x.Amount);
        return new AccountingProjectDto(project.Id, source.Name, project.Status, project.ArchivedAt.HasValue, boqCost, warehouseCost, staffSalary, workerCost, expenseCost,
            boqCost + warehouseCost + staffSalary + workerCost + expenseCost,
            source.OfferPrice, source.Location, source.Description, employeeMap.GetValueOrDefault(project.ProjectManagerAdminUserId) is { } manager ? DisplayName(manager) : "—", project.PlannedStartDate, project.PlannedEndDate,
            staff.OrderBy(x => x.AddedAt).Select(x => new AccountingStaffDto(x.Id, employeeMap.GetValueOrDefault(x.AdminUserId) is { } employee ? DisplayName(employee) : "Unknown", x.RoleName, employeeMap.GetValueOrDefault(x.AdminUserId)?.EmploymentStartDate, employeeMap.GetValueOrDefault(x.AdminUserId)?.SalaryPaymentDate, employeeMap.GetValueOrDefault(x.AdminUserId)?.MonthlySalary)).ToList(),
            boq.OrderBy(x => x.Id).Select(x => new AccountingBoqItemDto(x.Id, x.ItemName, x.Unit, x.PlannedQuantity, x.UnitCost, x.Note)).ToList(),
            workers.OrderBy(x => x.StartDate).Select(x => new AccountingWorkerDto(x.Id, x.FirstName, x.LastName, x.AmountPaid, x.StartDate, x.EndDate, x.Note)).ToList(),
            expenses.OrderByDescending(x => x.ExpenseDate).Select(x => new AccountingExpenseDto(x.Id, x.Category, x.Name, x.Amount, x.ExpenseDate, x.ReceiptUrl, x.Note, employeeMap.GetValueOrDefault(x.CreatedByAdminUserId) is { } creator ? DisplayName(creator) : "Sistem")).ToList());
    }

    private async Task<ProjectDetails> ResolveProjectDetailsAsync(ExecutionProject project, CancellationToken ct)
    {
        if (project.ProjectId is int publicId)
        {
            var source = await _uow.Repository<Project>().FirstOrDefaultNoTrackingAsync(x => x.Id == publicId, ct);
            if (source is not null)
            {
                var languages = await _uow.Repository<ProjectLanguage>().ListNoTrackingAsync(x => x.ProjectId == publicId && x.IsActive, ct);
                var language = languages.FirstOrDefault(x => x.LanguageCode == Volt.Domain.Enums.LanguageCode.AZ) ?? languages.FirstOrDefault();
                return new ProjectDetails(language?.Title ?? $"Project #{project.Id}", language?.Location ?? string.Empty, language?.Description ?? string.Empty, source.OfferAmountAzn);
            }
        }
        if (project.AdminTrackedProjectId is int trackedId)
        {
            var tracked = await _uow.Repository<AdminTrackedProject>().FirstOrDefaultNoTrackingAsync(x => x.Id == trackedId, ct);
            if (tracked is not null) return new ProjectDetails(tracked.Name, tracked.Location ?? string.Empty, tracked.Description ?? string.Empty, tracked.OfferPrice);
        }
        return new ProjectDetails($"Project #{project.Id}", string.Empty, string.Empty, null);
    }

    private sealed record ProjectDetails(string Name, string Location, string Description, decimal? OfferPrice);

    private static decimal AllocatedSalary(decimal? monthlySalary, DateTime? start, DateTime? end)
    {
        if (!monthlySalary.HasValue || monthlySalary <= 0 || !start.HasValue || !end.HasValue || end.Value < start.Value) return 0;
        return Math.Round(monthlySalary.Value / 30m * (end.Value.Date - start.Value.Date).Days + monthlySalary.Value / 30m, 2);
    }

    private static string DisplayName(AdminUser user) => string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
    private static string Normalize(string? value, int max, string fallback = "") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
