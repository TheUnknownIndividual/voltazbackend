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
    [HttpGet("me/status")]
    public async Task<IActionResult> GetMyStatus(CancellationToken ct)
    {
        var id = AdminId(); if (!id.HasValue) return Forbid();
        return CreateActionResult(await _service.GetStatusAsync(id.Value, ct));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{adminUserId:int}")]
    public async Task<IActionResult> CreateLink(int adminUserId, CancellationToken ct)
    {
        var actorId = AdminId(); if (!actorId.HasValue || (await _access.GetSessionAsync(actorId.Value, ct))?.IsSuperAdmin != true) return Forbid();
        return CreateActionResult(await _service.CreateLinkAsync(adminUserId, ct));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("{adminUserId:int}/status")]
    public async Task<IActionResult> GetStatus(int adminUserId, CancellationToken ct)
    {
        var actorId = AdminId();
        if (!actorId.HasValue || (await _access.GetSessionAsync(actorId.Value, ct))?.IsSuperAdmin != true) return Forbid();
        return CreateActionResult(await _service.GetStatusAsync(adminUserId, ct));
    }

    [AllowAnonymous]
    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] TelegramConnectionRedeemRequest request, CancellationToken ct)
    {
        if (!HasValidBotLinkKey()) return Unauthorized();
        return CreateActionResult(await _service.RedeemAsync(request, ct));
    }

    [AllowAnonymous]
    [HttpPost("analytics-subscriptions")]
    public async Task<IActionResult> SetAnalyticsSubscription(
        [FromBody] TelegramAnalyticsSubscriptionRequest request,
        CancellationToken ct)
    {
        if (!HasValidBotLinkKey()) return Unauthorized();
        return CreateActionResult(await _service.SetAnalyticsSubscriptionAsync(request, ct));
    }

    [AllowAnonymous]
    [HttpGet("analytics-subscriptions/{chatId:long}")]
    public async Task<IActionResult> GetAnalyticsSubscriptions(long chatId, CancellationToken ct)
    {
        if (!HasValidBotLinkKey()) return Unauthorized();
        return CreateActionResult(await _service.GetAnalyticsSubscriptionsAsync(chatId, ct));
    }

    [AllowAnonymous]
    [HttpPost("management/users")]
    public async Task<IActionResult> GetManagedUsers(
        [FromBody] TelegramManagementActorRequest request,
        CancellationToken ct)
    {
        if (!HasValidBotLinkKey()) return Unauthorized();
        return CreateActionResult(await _service.GetManagedUsersAsync(request.ManagerChatId, ct));
    }

    [AllowAnonymous]
    [HttpPost("management/users/{adminUserId:int}/link")]
    public async Task<IActionResult> CreateManagedLink(
        int adminUserId,
        [FromBody] TelegramManagementActorRequest request,
        CancellationToken ct)
    {
        if (!HasValidBotLinkKey()) return Unauthorized();
        return CreateActionResult(await _service.CreateManagedLinkAsync(request.ManagerChatId, adminUserId, ct));
    }

    [AllowAnonymous]
    [HttpPost("management/users/{adminUserId:int}/clear")]
    public async Task<IActionResult> ClearManagedChat(
        int adminUserId,
        [FromBody] TelegramManagementActorRequest request,
        CancellationToken ct)
    {
        if (!HasValidBotLinkKey()) return Unauthorized();
        return CreateActionResult(await _service.ClearManagedChatAsync(request.ManagerChatId, adminUserId, ct));
    }

    [AllowAnonymous]
    [HttpPost("management/users/{adminUserId:int}/assign")]
    public async Task<IActionResult> AssignManagedChat(
        int adminUserId,
        [FromBody] TelegramManagementAssignRequest request,
        CancellationToken ct)
    {
        if (!HasValidBotLinkKey()) return Unauthorized();
        return CreateActionResult(await _service.AssignManagedChatAsync(request.ManagerChatId, adminUserId, request.TelegramChatId, ct));
    }

    [AllowAnonymous]
    [HttpPost("management/users/{adminUserId:int}/subscriptions")]
    public async Task<IActionResult> SetManagedSubscription(
        int adminUserId,
        [FromBody] TelegramManagementSubscriptionRequest request,
        CancellationToken ct)
    {
        if (!HasValidBotLinkKey()) return Unauthorized();
        return CreateActionResult(await _service.SetManagedSubscriptionAsync(request.ManagerChatId, adminUserId, request.Topic, request.Enabled, ct));
    }

    private bool HasValidBotLinkKey()
    {
        var configured = Encoding.UTF8.GetBytes(_options.LinkApiKey ?? string.Empty);
        var supplied = Encoding.UTF8.GetBytes(Request.Headers["X-Volt-Bot-Link-Key"].ToString());
        return configured.Length > 0
            && configured.Length == supplied.Length
            && CryptographicOperations.FixedTimeEquals(configured, supplied);
    }

    private int? AdminId() => int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
}
