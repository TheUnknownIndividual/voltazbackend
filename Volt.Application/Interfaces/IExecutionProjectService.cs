using Volt.Application.Dtos;
using Volt.Application.Dtos.ExecutionProjects;

namespace Volt.Application.Interfaces;

public interface IExecutionProjectService
{
    Task<ApiResponse<ExecutionProjectBootstrapDto>> GetBootstrapAsync(CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<ExecutionProjectDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> CreateAsync(CreateExecutionProjectRequest request, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> UpdateAsync(int id, UpdateExecutionProjectRequest request, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> AddStaffAsync(int id, AddExecutionStaffRequest request, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> RemoveStaffAsync(int id, int staffId, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> AddExternalWorkerAsync(int id, AddExecutionExternalWorkerRequest request, int actorAdminUserId, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> RemoveExternalWorkerAsync(int id, int workerId, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> AddBoqItemAsync(int id, AddExecutionBoqItemRequest request, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> UpdateBoqItemAsync(int id, int boqItemId, AddExecutionBoqItemRequest request, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> RemoveBoqItemAsync(int id, int boqItemId, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> PrefillBoqAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> AddWarehouseMovementAsync(int id, AddExecutionWarehouseMovementRequest request, int actorAdminUserId, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> ApproveWarehouseMovementAsync(int id, int movementId, ApproveExecutionWarehouseMovementRequest request, int actorAdminUserId, bool actorCanApproveWarehouseMovements, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> AddTaskAsync(int id, AddExecutionTaskRequest request, int actorAdminUserId, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> RetryTaskNotificationAsync(int id, int taskId, CancellationToken ct = default);
    Task<ApiResponse<ExecutionProjectDto>> CompleteTaskAsync(int id, int taskId, int actorAdminUserId, CancellationToken ct = default);
}
