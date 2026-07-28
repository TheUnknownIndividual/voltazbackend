using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos.ExecutionProjects;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public sealed class ExecutionProjectsController : CustomBaseController
{
    private readonly IExecutionProjectService _service;
    private readonly IAdminAccessService _access;
    public ExecutionProjectsController(IExecutionProjectService service, IAdminAccessService access) { _service = service; _access = access; }

    [HttpGet("bootstrap")]
    public async Task<IActionResult> Bootstrap(CancellationToken ct) => CreateActionResult(await _service.GetBootstrapAsync(ct));
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => CreateActionResult(await _service.GetAllAsync(ct));
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExecutionProjectRequest request, CancellationToken ct) => CreateActionResult(await _service.CreateAsync(request, ct));
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExecutionProjectRequest request, CancellationToken ct) => CreateActionResult(await _service.UpdateAsync(id, request, ct));
    [HttpPost("{id:int}/staff")]
    public async Task<IActionResult> AddStaff(int id, [FromBody] AddExecutionStaffRequest request, CancellationToken ct) => CreateActionResult(await _service.AddStaffAsync(id, request, ct));
    [HttpDelete("{id:int}/staff/{staffId:int}")]
    public async Task<IActionResult> RemoveStaff(int id, int staffId, CancellationToken ct) => CreateActionResult(await _service.RemoveStaffAsync(id, staffId, ct));
    [HttpPost("{id:int}/external-workers")]
    public async Task<IActionResult> AddExternalWorker(int id, [FromBody] AddExecutionExternalWorkerRequest request, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue || !await _access.HasPageAsync(actorId.Value, Volt.Domain.Enums.AdminPage.Accounting, ct)) return Forbid();
        return CreateActionResult(await _service.AddExternalWorkerAsync(id, request, actorId.Value, ct));
    }
    [HttpDelete("{id:int}/external-workers/{workerId:int}")]
    public async Task<IActionResult> RemoveExternalWorker(int id, int workerId, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue || !await _access.HasPageAsync(actorId.Value, Volt.Domain.Enums.AdminPage.Accounting, ct)) return Forbid();
        return CreateActionResult(await _service.RemoveExternalWorkerAsync(id, workerId, ct));
    }
    [HttpPost("{id:int}/boq")]
    public async Task<IActionResult> AddBoq(int id, [FromBody] AddExecutionBoqItemRequest request, CancellationToken ct) => CreateActionResult(await _service.AddBoqItemAsync(id, request, ct));
    [HttpPut("{id:int}/boq/{boqItemId:int}")]
    public async Task<IActionResult> UpdateBoq(int id, int boqItemId, [FromBody] AddExecutionBoqItemRequest request, CancellationToken ct) => CreateActionResult(await _service.UpdateBoqItemAsync(id, boqItemId, request, ct));
    [HttpDelete("{id:int}/boq/{boqItemId:int}")]
    public async Task<IActionResult> RemoveBoq(int id, int boqItemId, CancellationToken ct) => CreateActionResult(await _service.RemoveBoqItemAsync(id, boqItemId, ct));
    [HttpPost("{id:int}/boq/prefill")]
    public async Task<IActionResult> PrefillBoq(int id, CancellationToken ct) => CreateActionResult(await _service.PrefillBoqAsync(id, ct));
    [HttpPost("{id:int}/warehouse-movements")]
    public async Task<IActionResult> AddMovement(int id, [FromBody] AddExecutionWarehouseMovementRequest request, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
        return CreateActionResult(await _service.AddWarehouseMovementAsync(id, request, actorId.Value, ct));
    }
    [HttpPost("{id:int}/warehouse-movements/{movementId:int}/approval")]
    public async Task<IActionResult> ApproveMovement(int id, int movementId, [FromBody] ApproveExecutionWarehouseMovementRequest request, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
        var session = await _access.GetSessionAsync(actorId.Value, ct); if (session is null) return Forbid();
        return CreateActionResult(await _service.ApproveWarehouseMovementAsync(id, movementId, request, actorId.Value, session.CanApproveWarehouseMovements, ct));
    }
    [HttpPost("{id:int}/tasks")]
    public async Task<IActionResult> AddTask(int id, [FromBody] AddExecutionTaskRequest request, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
        return CreateActionResult(await _service.AddTaskAsync(id, request, actorId.Value, ct));
    }
    [HttpPost("{id:int}/tasks/{taskId:int}/complete")]
    public async Task<IActionResult> CompleteTask(int id, int taskId, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
        return CreateActionResult(await _service.CompleteTaskAsync(id, taskId, actorId.Value, ct));
    }
    [HttpPost("{id:int}/tasks/{taskId:int}/retry-notification")]
    public async Task<IActionResult> RetryTaskNotification(int id, int taskId, CancellationToken ct) => CreateActionResult(await _service.RetryTaskNotificationAsync(id, taskId, ct));
    private int? AdminId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
