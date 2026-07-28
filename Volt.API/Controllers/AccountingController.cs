using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.API.Models;
using Volt.Application.Dtos;
using Volt.Application.Dtos.Accounting;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public sealed class AccountingController : CustomBaseController
{
    private readonly IAccountingService _service;
    private readonly IAdminAccessService _access;
    public AccountingController(IAccountingService service, IAdminAccessService access) { _service = service; _access = access; }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var actorId = AdminId();
        if (!actorId.HasValue || !await _access.HasPageAsync(actorId.Value, Volt.Domain.Enums.AdminPage.Accounting, ct)) return Forbid();
        return CreateActionResult(await _service.GetOverviewAsync(ct));
    }

    [HttpPut("employees/{employeeId:int}")]
    public async Task<IActionResult> UpdateEmployee(int employeeId, [FromBody] UpdateAccountingEmployeeRequest request, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue || !await _access.HasPageAsync(actorId.Value, Volt.Domain.Enums.AdminPage.Accounting, ct)) return Forbid();
        return CreateActionResult(await _service.UpdateEmployeeAsync(employeeId, request, ct));
    }

    [HttpPost("projects/{projectId:int}/expenses")]
    public async Task<IActionResult> AddExpense(int projectId, [FromBody] CreateAccountingExpenseRequest request, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue || !await _access.HasPageAsync(actorId.Value, Volt.Domain.Enums.AdminPage.Accounting, ct)) return Forbid();
        return CreateActionResult(await _service.AddExpenseAsync(projectId, request, actorId.Value, ct));
    }

    [HttpDelete("projects/{projectId:int}/expenses/{expenseId:int}")]
    public async Task<IActionResult> DeleteExpense(int projectId, int expenseId, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue || !await _access.HasPageAsync(actorId.Value, Volt.Domain.Enums.AdminPage.Accounting, ct)) return Forbid();
        return CreateActionResult(await _service.DeleteExpenseAsync(projectId, expenseId, ct));
    }

    [HttpPost("projects/{projectId:int}/receipt")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadReceipt(int projectId, [FromForm] UploadImageFormRequest form, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue || !await _access.HasPageAsync(actorId.Value, Volt.Domain.Enums.AdminPage.Accounting, ct)) return Forbid();
        if (form?.File is null) return BadRequest(new { success = false, error = "Receipt image is required." });
        await using var stream = new MemoryStream();
        await form.File.CopyToAsync(stream, ct); stream.Position = 0;
        return CreateActionResult(await _service.UploadReceiptAsync(projectId, new FileUploadRequest { FileName = form.File.FileName, Content = stream }, ct));
    }

    private int? AdminId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
