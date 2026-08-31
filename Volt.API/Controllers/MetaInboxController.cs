#nullable enable

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos.MetaInbox;
using Volt.Application.Interfaces;
using Volt.API.Services;
using Volt.Domain.Enums;

namespace Volt.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/meta-inbox")]
    [ApiController]
    public sealed class MetaInboxController : CustomBaseController
    {
        private readonly IMetaInboxService _service;
        private readonly IMetaWhatsAppOnboardingService _whatsAppOnboarding;
        private readonly IAdminAccessService _adminAccess;
        private readonly MetaInboxOptions _options;
        private readonly MetaWebhookDiagnostics _webhookDiagnostics;
        private readonly ILogger<MetaInboxController> _logger;

        public MetaInboxController(
            IMetaInboxService service,
            IMetaWhatsAppOnboardingService whatsAppOnboarding,
            IAdminAccessService adminAccess,
            IOptions<MetaInboxOptions> options,
            MetaWebhookDiagnostics webhookDiagnostics,
            ILogger<MetaInboxController> logger)
        {
            _service = service;
            _whatsAppOnboarding = whatsAppOnboarding;
            _adminAccess = adminAccess;
            _options = options.Value;
            _webhookDiagnostics = webhookDiagnostics;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? verifyToken,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            if (!_options.Enabled || mode != "subscribe" || string.IsNullOrWhiteSpace(challenge) || !FixedTimeEquals(_options.VerifyToken, verifyToken))
                return Unauthorized();
            return Content(challenge, "text/plain", Encoding.UTF8);
        }

        [AllowAnonymous]
        [HttpPost("webhook")]
        [EnableRateLimiting("meta-webhook")]
        [RequestSizeLimit(1_048_576)]
        public async Task<IActionResult> ReceiveWebhook(CancellationToken ct)
        {
            var attemptId = _webhookDiagnostics.BeginAttempt();
            try
            {
                if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.AppSecret))
                {
                    _webhookDiagnostics.CompleteRejected(attemptId, StatusCodes.Status404NotFound, "not_configured");
                    return NotFound();
                }

                await using var body = new MemoryStream();
                await Request.Body.CopyToAsync(body, ct);
                var payloadBytes = body.ToArray();
                if (!ValidSignature(payloadBytes, Request.Headers["X-Hub-Signature-256"].ToString()))
                {
                    _webhookDiagnostics.CompleteRejected(attemptId, StatusCodes.Status401Unauthorized, "invalid_signature");
                    _logger.LogWarning("Meta webhook POST rejected because its signature was invalid.");
                    return Unauthorized();
                }

                var payload = Encoding.UTF8.GetString(payloadBytes);
                var processingResult = await _service.ProcessWebhookAsync(payload, ct);
                _webhookDiagnostics.CompleteAccepted(attemptId, processingResult);
                return Ok();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _webhookDiagnostics.CompleteFailed(attemptId, 499, "request_cancelled");
                throw;
            }
            catch (Exception exception)
            {
                _webhookDiagnostics.CompleteFailed(attemptId, StatusCodes.Status500InternalServerError, "processing_failed");
                _logger.LogError(exception, "Meta webhook POST failed during processing.");
                throw;
            }
        }

        [HttpGet("configuration")]
        public IActionResult Configuration()
        {
            var commonReady = _options.Enabled && !string.IsNullOrWhiteSpace(_options.AppSecret) &&
                              !string.IsNullOrWhiteSpace(_options.VerifyToken);
            var messengerReady = commonReady && !string.IsNullOrWhiteSpace(_options.PageAccessToken);
            var whatsAppReady = commonReady && !string.IsNullOrWhiteSpace(_options.WhatsAppAccessToken) &&
                                !string.IsNullOrWhiteSpace(_options.WhatsAppPhoneNumberId);
            return Ok(new
            {
                success = true,
                data = new MetaInboxConfigurationDto(
                    _options.Enabled,
                    messengerReady || whatsAppReady,
                    _options.GraphApiVersion,
                    messengerReady,
                    whatsAppReady,
                    _webhookDiagnostics.Snapshot())
            });
        }

        [HttpGet("whatsapp-onboarding")]
        public async Task<IActionResult> WhatsAppOnboardingStatus(CancellationToken ct)
        {
            if (!await CanManageWhatsAppOnboardingAsync(ct)) return Forbid();
            return Ok(new { success = true, data = await _whatsAppOnboarding.GetStatusAsync(ct), error = (object?)null });
        }

        [HttpPost("whatsapp-onboarding/complete")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> CompleteWhatsAppOnboarding([FromBody] WhatsAppOnboardingCompleteRequest request, CancellationToken ct)
        {
            var actorId = AdminId();
            if (!actorId.HasValue || !await CanManageWhatsAppOnboardingAsync(ct)) return Forbid();
            return CreateActionResult(await _whatsAppOnboarding.CompleteAsync(request, actorId.Value, ct));
        }

        [HttpPost("whatsapp-onboarding/register")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> RegisterWhatsAppPhone([FromBody] WhatsAppOnboardingRegisterRequest request, CancellationToken ct)
        {
            var actorId = AdminId();
            if (!actorId.HasValue || !await CanManageWhatsAppOnboardingAsync(ct)) return Forbid();
            return CreateActionResult(await _whatsAppOnboarding.RegisterAsync(request, actorId.Value, ct));
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> Conversations([FromQuery] string? search, [FromQuery] string? status, [FromQuery] string? assignment, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        {
            var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
            return CreateActionResult(await _service.GetConversationsAsync(search, status, assignment, actorId.Value, page, pageSize, ct));
        }

        [HttpGet("conversations/{conversationId:int}/messages")]
        public async Task<IActionResult> Messages(int conversationId, [FromQuery] long? afterId, CancellationToken ct)
            => CreateActionResult(await _service.GetMessagesAsync(conversationId, afterId, ct));

        [HttpGet("conversations/{conversationId:int}/notes")]
        public async Task<IActionResult> Notes(int conversationId, [FromQuery] long? afterId, CancellationToken ct)
            => CreateActionResult(await _service.GetNotesAsync(conversationId, afterId, ct));

        [HttpGet("assignees")]
        public async Task<IActionResult> Assignees(CancellationToken ct)
            => CreateActionResult(await _service.GetAssigneesAsync(ct));

        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount(CancellationToken ct)
            => CreateActionResult(await _service.GetUnreadCountAsync(ct));

        [HttpPut("conversations/{conversationId:int}/assignment")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> Assign(int conversationId, [FromBody] MetaInboxAssignRequest request, CancellationToken ct)
        {
            var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
            return CreateActionResult(await _service.AssignAsync(conversationId, request.AssignedAdminUserId, actorId.Value, ct));
        }

        [HttpPut("conversations/{conversationId:int}/status")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> Status(int conversationId, [FromBody] MetaInboxStatusRequest request, CancellationToken ct)
        {
            var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
            return CreateActionResult(await _service.UpdateStatusAsync(conversationId, request.Status, actorId.Value, ct));
        }

        [HttpPut("conversations/{conversationId:int}/read")]
        public async Task<IActionResult> MarkRead(int conversationId, CancellationToken ct)
            => CreateActionResult(await _service.MarkReadAsync(conversationId, ct));

        [HttpPost("conversations/{conversationId:int}/messages")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> SendMessage(int conversationId, [FromBody] MetaInboxSendMessageRequest request, CancellationToken ct)
        {
            var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
            return CreateActionResult(await _service.SendMessageAsync(conversationId, request.Text, actorId.Value, ct));
        }

        [HttpPost("conversations/{conversationId:int}/notes")]
        [EnableRateLimiting("protected-write")]
        public async Task<IActionResult> AddNote(int conversationId, [FromBody] MetaInboxAddNoteRequest request, CancellationToken ct)
        {
            var actorId = AdminId(); if (!actorId.HasValue) return Forbid();
            return CreateActionResult(await _service.AddNoteAsync(conversationId, request.Body, actorId.Value, ct));
        }

        private int? AdminId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private async Task<bool> CanManageWhatsAppOnboardingAsync(CancellationToken ct)
        {
            var actorId = AdminId();
            if (!actorId.HasValue) return false;
            var session = await _adminAccess.GetSessionAsync(actorId.Value, ct);
            return session is not null &&
                   (session.IsSuperAdmin || session.AllowedPages.Contains(AdminPage.WhatsAppOnboarding));
        }

        private bool ValidSignature(byte[] payload, string signatureHeader)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;
            byte[] supplied;
            try { supplied = Convert.FromHexString(signatureHeader[7..]); }
            catch (FormatException) { return false; }
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.AppSecret));
            var expected = hmac.ComputeHash(payload);
            return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
        }

        private static bool FixedTimeEquals(string expected, string? supplied)
        {
            var expectedBytes = Encoding.UTF8.GetBytes(expected ?? string.Empty);
            var suppliedBytes = Encoding.UTF8.GetBytes(supplied ?? string.Empty);
            return expectedBytes.Length > 0 && expectedBytes.Length == suppliedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
        }
    }
}
