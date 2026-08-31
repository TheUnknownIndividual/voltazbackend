using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Volt.Application.Configuration;
using Volt.Application.Dtos.SolarInverter;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public sealed class SolarInvertersController : CustomBaseController
{
    private readonly ISolarInverterService _service;
    private readonly ISolarInverterDatasheetQaService _qaService;
    private readonly SolarInverterQaOptions _qaOptions;

    public SolarInvertersController(
        ISolarInverterService service,
        ISolarInverterDatasheetQaService qaService,
        IOptions<SolarInverterQaOptions> qaOptions)
    {
        _service = service;
        _qaService = qaService;
        _qaOptions = qaOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? systemType,
        [FromQuery] string? phase,
        CancellationToken ct)
        => CreateActionResult(await _service.GetAllAsync(systemType, phase, ct));

    [HttpPost("datasheets/import")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ImportDatasheets(
        [FromBody] SolarInverterDatasheetImportRequest request,
        CancellationToken ct)
        => CreateActionResult(await _service.ImportDatasheetsAsync(request, ct));

    [HttpGet("datasheets/qa")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDatasheetQaList(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (!_qaOptions.Enabled) return NotFound();

        return CreateActionResult(await _qaService.GetListAsync(
            status,
            search,
            page,
            pageSize,
            ct));
    }

    [HttpGet("datasheets/qa/{specificationId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDatasheetQaDetail(
        int specificationId,
        CancellationToken ct)
    {
        if (!_qaOptions.Enabled) return NotFound();

        return CreateActionResult(await _qaService.GetDetailAsync(specificationId, ct));
    }

    [HttpPut("datasheets/qa/{specificationId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDatasheetQa(
        int specificationId,
        [FromBody] SolarInverterQaUpdateRequest request,
        CancellationToken ct)
    {
        if (!_qaOptions.Enabled) return NotFound();

        return CreateActionResult(await _qaService.UpdateAsync(
            specificationId,
            GetAdminUserId(),
            request,
            ct));
    }

    [HttpPost("datasheets/qa/{specificationId:int}/done")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CompleteDatasheetQa(
        int specificationId,
        CancellationToken ct)
    {
        if (!_qaOptions.Enabled) return NotFound();

        return CreateActionResult(await _qaService.DoneAsync(
            specificationId,
            GetAdminUserId(),
            ct));
    }

    private int GetAdminUserId()
        => int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out var adminUserId)
            ? adminUserId
            : 0;
}
