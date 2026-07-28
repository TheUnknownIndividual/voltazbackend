using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Configuration;
using Volt.Application.Dtos.Admin;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class AdminTelegramConnectionsController : CustomBaseController
{
    private readonly IAdminTelegramConnectionService _service;
    private readonly IAdminAccessService _access;
    private readonly TelegramBotOptions _options;
    public AdminTelegramConnectionsController(IAdminTelegramConnectionService service, IAdminAccessService access, Microsoft.Extensions.Options.IOptions<TelegramBotOptions> options) { _service = service; _access = access; _options = options.Value; }

    [Authorize(Roles = "Admin")]
    [HttpPost("me")]
    public async Task<IActionResult> CreateMyLink(CancellationToken ct)
    {
        var id = AdminId(); if (!id.HasValue) return Forbid();
        return CreateActionResult(await _service.CreateLinkAsync(id.Value, ct));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{adminUserId:int}")]
    public async Task<IActionResult> CreateLink(int adminUserId, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue || (await _access.GetSessionAsync(actorId.Value, ct))?.IsSuperAdmin != true) return Forbid();
        return CreateActionResult(await _service.CreateLinkAsync(adminUserId, ct));
    }

    [AllowAnonymous]
    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] TelegramConnectionRedeemRequest request, CancellationToken ct)
    {
        var configured = Encoding.UTF8.GetBytes(_options.LinkApiKey ?? string.Empty);
        var supplied = Encoding.UTF8.GetBytes(Request.Headers["X-Volt-Bot-Link-Key"].ToString());
        if (configured.Length == 0 || configured.Length != supplied.Length || !CryptographicOperations.FixedTimeEquals(configured, supplied)) return Unauthorized();
        return CreateActionResult(await _service.RedeemAsync(request, ct));
    }

    private int? AdminId() => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
