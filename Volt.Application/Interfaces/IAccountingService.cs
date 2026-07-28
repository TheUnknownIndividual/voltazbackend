using Volt.Application.Dtos;
using Volt.Application.Dtos.Accounting;

namespace Volt.Application.Interfaces;

public interface IAccountingService
{
    Task<ApiResponse<AccountingOverviewDto>> GetOverviewAsync(CancellationToken ct = default);
    Task<ApiResponse<AccountingOverviewDto>> UpdateEmployeeAsync(int employeeId, UpdateAccountingEmployeeRequest request, CancellationToken ct = default);
    Task<ApiResponse<AccountingOverviewDto>> AddExpenseAsync(int projectId, CreateAccountingExpenseRequest request, int actorAdminUserId, CancellationToken ct = default);
    Task<ApiResponse<AccountingOverviewDto>> DeleteExpenseAsync(int projectId, int expenseId, CancellationToken ct = default);
    Task<ApiResponse<string>> UploadReceiptAsync(int projectId, FileUploadRequest file, CancellationToken ct = default);
}
