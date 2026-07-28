using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Configuration;
using Volt.Application.Interfaces;

namespace Volt.API.Controllers;

[Route("api/StakeholderProjectApprovals")]
[ApiController]
public sealed class StakeholderProjectApprovalsController : CustomBaseController
{
    private readonly IAdminProjectTrackerService _projects;
    private readonly TelegramBotOptions _options;
    public StakeholderProjectApprovalsController(IAdminProjectTrackerService projects, Microsoft.Extensions.Options.IOptions<TelegramBotOptions> options) { _projects = projects; _options = options.Value; }

    [AllowAnonymous]
    [HttpPost("{requestId:int}/decision")]
    public async Task<IActionResult> Decision(int requestId, [FromBody] StakeholderProjectDecisionRequest request, CancellationToken ct)
    {
        var configured = Encoding.UTF8.GetBytes(_options.LinkApiKey ?? string.Empty);
        var supplied = Encoding.UTF8.GetBytes(Request.Headers["X-Volt-Bot-Link-Key"].ToString());
        if (configured.Length == 0 || configured.Length != supplied.Length || !CryptographicOperations.FixedTimeEquals(configured, supplied)) return Unauthorized();
        return CreateActionResult(await _projects.RecordStakeholderDecisionAsync(requestId, request.ChatId, request.Approved, ct));
    }
}

public sealed class StakeholderProjectDecisionRequest { public long ChatId { get; set; } public bool Approved { get; set; } }
