#nullable enable

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Dtos;
using Volt.Application.Dtos.MetaInbox;
using Volt.Application.Interfaces;
using Volt.Domain.Common;
using Volt.Domain.Entities;
using Volt.Domain.Enums;
using Volt.Infrastructure.Data;

namespace Volt.Infrastructure.Services
{
    public sealed class MetaInboxService : IMetaInboxService
    {
        private readonly DataContext _context;
        private readonly HttpClient _client;
        private readonly MetaInboxOptions _options;
        private readonly IAdminAuditService _audit;

        public MetaInboxService(DataContext context, HttpClient client, IOptions<MetaInboxOptions> options, IAdminAuditService audit)
        {
            _context = context;
            _client = client;
            _options = options.Value;
            _audit = audit;
        }

        public async Task<MetaInboxWebhookProcessingResult> ProcessWebhookAsync(string payload, CancellationToken ct = default)
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var objectName = ReadString(root, "object");
            var processingResult = SummarizeWebhook(root, objectName);
            if (objectName == "whatsapp_business_account")
            {
                await ProcessWhatsAppWebhookAsync(root, ct);
                return processingResult;
            }

            var channel = objectName switch
            {
                "page" => MetaInboxChannel.Messenger,
                "instagram" => MetaInboxChannel.Instagram,
                _ => (MetaInboxChannel?)null
            };
            if (!channel.HasValue || !root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return processingResult;

            var remainingProfileLookups = 3;
            using var profileLookupTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            profileLookupTimeout.CancelAfter(TimeSpan.FromSeconds(2));
            foreach (var entry in entries.EnumerateArray())
            {
                var accountId = ReadString(entry, "id");
                if (string.IsNullOrWhiteSpace(accountId) || !entry.TryGetProperty("messaging", out var events) || events.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var messagingEvent in events.EnumerateArray())
                {
                    var senderId = messagingEvent.TryGetProperty("sender", out var sender) ? ReadString(sender, "id") : null;
                    var recipientId = messagingEvent.TryGetProperty("recipient", out var recipient) ? ReadString(recipient, "id") : null;

                    if (messagingEvent.TryGetProperty("delivery", out var deliveryElement))
                    {
                        await ApplyDeliveryStatusAsync(channel.Value, accountId, senderId, deliveryElement, ct);
                        continue;
                    }

                    if (messagingEvent.TryGetProperty("read", out var readElement))
                    {
                        await ApplyReadStatusAsync(channel.Value, accountId, senderId, readElement, ct);
                        continue;
                    }

                    if (!messagingEvent.TryGetProperty("message", out var messageElement))
                        continue;

                    var isEcho = messageElement.TryGetProperty("is_echo", out var echoElement) && echoElement.ValueKind == JsonValueKind.True;
                    var participantId = isEcho ? recipientId : senderId;
                    if (string.IsNullOrWhiteSpace(participantId))
                        continue;

                    var externalMessageId = ReadString(messageElement, "mid");
                    var text = ReadString(messageElement, "text");
                    var attachments = ReadAttachments(messageElement);
                    var occurredAt = ReadTimestamp(messagingEvent);
                    var direction = isEcho ? MetaInboxMessageDirection.Outgoing : MetaInboxMessageDirection.Incoming;

                    var conversation = await _context.MetaInboxConversations
                        .FirstOrDefaultAsync(x => x.Channel == channel.Value && x.AccountExternalId == accountId && x.ParticipantExternalId == participantId, ct);
                    var isNewConversation = conversation is null;
                    if (conversation is null)
                    {
                        conversation = new MetaInboxConversation
                        {
                            Channel = channel.Value,
                            AccountExternalId = accountId,
                            ParticipantExternalId = participantId,
                            ParticipantDisplayName = participantId,
                            Status = "open",
                            CreatedAt = occurredAt,
                            UpdatedAt = occurredAt,
                            LastMessageAt = occurredAt
                        };
                        _context.MetaInboxConversations.Add(conversation);
                        try
                        {
                            await _context.SaveChangesAsync(ct);
                        }
                        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                        {
                            _context.Entry(conversation).State = EntityState.Detached;
                            conversation = await _context.MetaInboxConversations.FirstAsync(
                                x => x.Channel == channel.Value && x.AccountExternalId == accountId && x.ParticipantExternalId == participantId, ct);
                            isNewConversation = false;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(externalMessageId) && await _context.MetaInboxMessages
                            .AnyAsync(x => x.ConversationId == conversation.Id && x.ExternalMessageId == externalMessageId, ct))
                        continue;

                    var message = new MetaInboxMessage
                    {
                        ConversationId = conversation.Id,
                        ExternalMessageId = externalMessageId,
                        Direction = direction,
                        Text = TrimOrNull(text, 4000),
                        AttachmentsJson = JsonSerializer.Serialize(attachments),
                        DeliveryStatus = direction == MetaInboxMessageDirection.Incoming ? "received" : "sent",
                        CreatedAt = occurredAt
                    };
                    _context.MetaInboxMessages.Add(message);
                    if (direction == MetaInboxMessageDirection.Incoming)
                    {
                        conversation.UnreadCount++;
                        conversation.Status = "open";
                    }
                    if (occurredAt >= conversation.LastMessageAt)
                    {
                        conversation.LastMessageAt = occurredAt;
                        conversation.LastMessagePreview = BuildPreview(text, attachments);
                    }
                    conversation.UpdatedAt = DateTime.UtcNow;
                    try
                    {
                        await _context.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                    {
                        _context.Entry(message).State = EntityState.Detached;
                        await _context.Entry(conversation).ReloadAsync(ct);
                    }

                    if (isNewConversation && remainingProfileLookups-- > 0 && !profileLookupTimeout.IsCancellationRequested)
                        await TryPopulateProfileAsync(conversation, profileLookupTimeout.Token);
                }
            }

            return processingResult;
        }

        private static MetaInboxWebhookProcessingResult SummarizeWebhook(JsonElement root, string? objectName)
        {
            string? field = null;
            string? phoneNumberId = null;
            var messageCount = 0;
            var statusCount = 0;

            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return new MetaInboxWebhookProcessingResult(objectName, field, phoneNumberId, messageCount, statusCount);

            foreach (var entry in entries.EnumerateArray())
            {
                if (objectName == "whatsapp_business_account" &&
                    entry.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        field ??= ReadString(change, "field");
                        if (!change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
                            continue;

                        if (value.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
                            phoneNumberId ??= ReadString(metadata, "phone_number_id");
                        if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                            foreach (var _ in messages.EnumerateArray()) messageCount++;
                        if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                            foreach (var _ in statuses.EnumerateArray()) statusCount++;
                    }
                    continue;
                }

                if (!entry.TryGetProperty("messaging", out var events) || events.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var messagingEvent in events.EnumerateArray())
                {
                    if (messagingEvent.TryGetProperty("message", out _)) messageCount++;
                    if (messagingEvent.TryGetProperty("delivery", out _) || messagingEvent.TryGetProperty("read", out _)) statusCount++;
                }
            }

            return new MetaInboxWebhookProcessingResult(objectName, field, phoneNumberId, messageCount, statusCount);
        }

        private async Task ProcessWhatsAppWebhookAsync(JsonElement root, CancellationToken ct)
        {
            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (ReadString(change, "field") != "messages" ||
                        !change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
                        continue;

                    var phoneNumberId = value.TryGetProperty("metadata", out var metadata)
                        ? ReadString(metadata, "phone_number_id")
                        : null;
                    if (string.IsNullOrWhiteSpace(phoneNumberId) ||
                        (!string.IsNullOrWhiteSpace(_options.WhatsAppPhoneNumberId) &&
                         !string.Equals(phoneNumberId, _options.WhatsAppPhoneNumberId, StringComparison.Ordinal)))
                        continue;

                    var contactNames = ReadWhatsAppContactNames(value);
                    if (value.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var incoming in messages.EnumerateArray())
                            await StoreWhatsAppMessageAsync(phoneNumberId, incoming, contactNames, ct);
                    }

                    if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var status in statuses.EnumerateArray())
                            await ApplyWhatsAppStatusAsync(phoneNumberId, status, ct);
                    }
                }
            }
        }

        private async Task StoreWhatsAppMessageAsync(
            string phoneNumberId,
            JsonElement incoming,
            IReadOnlyDictionary<string, string> contactNames,
            CancellationToken ct)
        {
            var participantId = ReadString(incoming, "from");
            if (string.IsNullOrWhiteSpace(participantId)) return;

            var externalMessageId = ReadString(incoming, "id");
            var messageType = ReadString(incoming, "type") ?? "message";
            var text = ReadWhatsAppText(incoming, messageType);
            var attachments = ReadWhatsAppAttachments(incoming, messageType);
            var occurredAt = ReadWhatsAppTimestamp(incoming);
            contactNames.TryGetValue(participantId, out var contactName);

            var conversation = await _context.MetaInboxConversations.FirstOrDefaultAsync(
                x => x.Channel == MetaInboxChannel.WhatsApp && x.AccountExternalId == phoneNumberId &&
                     x.ParticipantExternalId == participantId, ct);
            var isNewConversation = conversation is null;
            if (conversation is null)
            {
                conversation = new MetaInboxConversation
                {
                    Channel = MetaInboxChannel.WhatsApp,
                    AccountExternalId = phoneNumberId,
                    ParticipantExternalId = participantId,
                    ParticipantDisplayName = TrimOrNull(contactName, 200) ?? participantId,
                    Status = "open",
                    CreatedAt = occurredAt,
                    UpdatedAt = occurredAt,
                    LastMessageAt = occurredAt
                };
                _context.MetaInboxConversations.Add(conversation);
                try
                {
                    await _context.SaveChangesAsync(ct);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception))
                {
                    _context.Entry(conversation).State = EntityState.Detached;
                    conversation = await _context.MetaInboxConversations.FirstAsync(
                        x => x.Channel == MetaInboxChannel.WhatsApp && x.AccountExternalId == phoneNumberId &&
                             x.ParticipantExternalId == participantId, ct);
                    isNewConversation = false;
                }
            }

            if (!string.IsNullOrWhiteSpace(contactName) &&
                (isNewConversation || string.Equals(conversation.ParticipantDisplayName, participantId, StringComparison.Ordinal)))
                conversation.ParticipantDisplayName = Trim(contactName, 200);

            if (!string.IsNullOrWhiteSpace(externalMessageId) && await _context.MetaInboxMessages
                    .AnyAsync(x => x.ConversationId == conversation.Id && x.ExternalMessageId == externalMessageId, ct))
                return;

            var message = new MetaInboxMessage
            {
                ConversationId = conversation.Id,
                ExternalMessageId = externalMessageId,
                Direction = MetaInboxMessageDirection.Incoming,
                Text = TrimOrNull(text, 4000),
                AttachmentsJson = JsonSerializer.Serialize(attachments),
                DeliveryStatus = "received",
                CreatedAt = occurredAt
            };
            _context.MetaInboxMessages.Add(message);
            conversation.UnreadCount++;
            conversation.Status = "open";
            if (occurredAt >= conversation.LastMessageAt)
            {
                conversation.LastMessageAt = occurredAt;
                conversation.LastMessagePreview = BuildPreview(text, attachments);
            }
            conversation.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                _context.Entry(message).State = EntityState.Detached;
                await _context.Entry(conversation).ReloadAsync(ct);
            }
        }

        private async Task ApplyWhatsAppStatusAsync(string phoneNumberId, JsonElement statusElement, CancellationToken ct)
        {
            var externalMessageId = ReadString(statusElement, "id");
            var participantId = ReadString(statusElement, "recipient_id");
            var status = ReadString(statusElement, "status")?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(externalMessageId) || string.IsNullOrWhiteSpace(participantId) ||
                status is not ("sent" or "delivered" or "read" or "failed" or "deleted"))
                return;

            var message = await _context.MetaInboxMessages
                .Include(x => x.Conversation)
                .FirstOrDefaultAsync(x =>
                    x.Conversation.Channel == MetaInboxChannel.WhatsApp &&
                    x.Conversation.AccountExternalId == phoneNumberId &&
                    x.Conversation.ParticipantExternalId == participantId &&
                    x.ExternalMessageId == externalMessageId, ct);
            if (message is null || message.Direction != MetaInboxMessageDirection.Outgoing) return;

            if (status == "failed" || status == "deleted" || DeliveryStatusRank(status) > DeliveryStatusRank(message.DeliveryStatus))
            {
                message.DeliveryStatus = status;
                await _context.SaveChangesAsync(ct);
            }
        }

        private async Task ApplyDeliveryStatusAsync(MetaInboxChannel channel, string accountId, string? participantId, JsonElement delivery, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(participantId)) return;
            var conversationId = await _context.MetaInboxConversations.AsNoTracking()
                .Where(x => x.Channel == channel && x.AccountExternalId == accountId && x.ParticipantExternalId == participantId)
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (!conversationId.HasValue) return;

            var messageIds = new List<string>();
            if (delivery.TryGetProperty("mids", out var mids) && mids.ValueKind == JsonValueKind.Array)
            {
                messageIds.AddRange(mids.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))!
                    .Select(x => x!));
            }

            var messages = _context.MetaInboxMessages.Where(x =>
                x.ConversationId == conversationId.Value &&
                x.Direction == MetaInboxMessageDirection.Outgoing &&
                x.DeliveryStatus != "read");
            if (messageIds.Count > 0)
                messages = messages.Where(x => x.ExternalMessageId != null && messageIds.Contains(x.ExternalMessageId));
            else if (ReadUnixMilliseconds(delivery, "watermark") is { } deliveredAt)
                messages = messages.Where(x => x.CreatedAt <= deliveredAt);
            else
                return;

            await messages.ExecuteUpdateAsync(updates => updates.SetProperty(x => x.DeliveryStatus, "delivered"), ct);
        }

        private async Task ApplyReadStatusAsync(MetaInboxChannel channel, string accountId, string? participantId, JsonElement read, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(participantId) || ReadUnixMilliseconds(read, "watermark") is not { } readAt) return;
            var conversationId = await _context.MetaInboxConversations.AsNoTracking()
                .Where(x => x.Channel == channel && x.AccountExternalId == accountId && x.ParticipantExternalId == participantId)
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (!conversationId.HasValue) return;

            await _context.MetaInboxMessages
                .Where(x => x.ConversationId == conversationId.Value &&
                            x.Direction == MetaInboxMessageDirection.Outgoing &&
                            x.CreatedAt <= readAt && x.DeliveryStatus != "read")
                .ExecuteUpdateAsync(updates => updates.SetProperty(x => x.DeliveryStatus, "read"), ct);
        }

        public async Task<ApiResponse<MetaInboxConversationPageDto>> GetConversationsAsync(string? search, string? status, string? assignment, int actorAdminUserId, int page, int pageSize, CancellationToken ct = default)
        {
            var query = _context.MetaInboxConversations.AsNoTracking().Include(x => x.AssignedAdminUser).AsQueryable();
            var normalizedStatus = NormalizeStatus(status, allowAll: true);
            if (normalizedStatus is not null) query = query.Where(x => x.Status == normalizedStatus);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x => x.ParticipantDisplayName.Contains(term) || x.ParticipantExternalId.Contains(term) || x.LastMessagePreview.Contains(term));
            }
            switch (assignment?.Trim().ToLowerInvariant())
            {
                case "mine": query = query.Where(x => x.AssignedAdminUserId == actorAdminUserId); break;
                case "unassigned": query = query.Where(x => x.AssignedAdminUserId == null); break;
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var total = await query.CountAsync(ct);
            var unreadTotal = await query.SumAsync(x => (int?)x.UnreadCount, ct) ?? 0;
            var records = await query.OrderByDescending(x => x.LastMessageAt).ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return ApiResponse<MetaInboxConversationPageDto>.SuccessResponse(
                new MetaInboxConversationPageDto(records.Select(MapConversation).ToList(), total, unreadTotal, page, pageSize));
        }

        public async Task<ApiResponse<IReadOnlyList<MetaInboxMessageDto>>> GetMessagesAsync(int conversationId, long? afterId, CancellationToken ct = default)
        {
            if (!await _context.MetaInboxConversations.AsNoTracking().AnyAsync(x => x.Id == conversationId, ct))
                return NotFound<IReadOnlyList<MetaInboxMessageDto>>();
            var query = _context.MetaInboxMessages.AsNoTracking().Include(x => x.SentByAdminUser)
                .Where(x => x.ConversationId == conversationId);
            List<MetaInboxMessage> messages;
            if (afterId.HasValue)
                messages = await query.Where(x => x.Id > afterId.Value).OrderBy(x => x.Id).Take(200).ToListAsync(ct);
            else
            {
                messages = await query.OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);
                messages.Reverse();
            }
            return ApiResponse<IReadOnlyList<MetaInboxMessageDto>>.SuccessResponse(messages.Select(MapMessage).ToList());
        }

        public async Task<ApiResponse<IReadOnlyList<MetaInboxInternalNoteDto>>> GetNotesAsync(int conversationId, long? afterId, CancellationToken ct = default)
        {
            if (!await _context.MetaInboxConversations.AsNoTracking().AnyAsync(x => x.Id == conversationId, ct))
                return NotFound<IReadOnlyList<MetaInboxInternalNoteDto>>();
            var query = _context.MetaInboxInternalNotes.AsNoTracking().Include(x => x.AuthorAdminUser)
                .Where(x => x.ConversationId == conversationId);
            List<MetaInboxInternalNote> notes;
            if (afterId.HasValue)
                notes = await query.Where(x => x.Id > afterId.Value).OrderBy(x => x.Id).Take(100).ToListAsync(ct);
            else
            {
                notes = await query.OrderByDescending(x => x.Id).Take(100).ToListAsync(ct);
                notes.Reverse();
            }
            return ApiResponse<IReadOnlyList<MetaInboxInternalNoteDto>>.SuccessResponse(notes.Select(MapNote).ToList());
        }

        public async Task<ApiResponse<IReadOnlyList<MetaInboxAssigneeDto>>> GetAssigneesAsync(CancellationToken ct = default)
        {
            var admins = await _context.AdminUsers.AsNoTracking()
                .Where(x => x.IsActive && (x.IsSuperAdmin || x.Username == "admin" || x.PagePermissions.Any(p => p.Page == AdminPage.MessageInbox)))
                .OrderBy(x => x.DisplayName).ThenBy(x => x.Username).ToListAsync(ct);
            return ApiResponse<IReadOnlyList<MetaInboxAssigneeDto>>.SuccessResponse(admins
                .Select(x => new MetaInboxAssigneeDto(x.Id, string.IsNullOrWhiteSpace(x.DisplayName) ? x.Username : x.DisplayName)).ToList());
        }

        public async Task<ApiResponse<int>> GetUnreadCountAsync(CancellationToken ct = default)
            => ApiResponse<int>.SuccessResponse(await _context.MetaInboxConversations.AsNoTracking().SumAsync(x => (int?)x.UnreadCount, ct) ?? 0);

        public async Task<ApiResponse<MetaInboxConversationDto>> AssignAsync(int conversationId, int? assignedAdminUserId, int actorAdminUserId, CancellationToken ct = default)
        {
            var conversation = await FindConversationAsync(conversationId, ct);
            if (conversation is null) return NotFound<MetaInboxConversationDto>();
            AdminUser? assignee = null;
            if (assignedAdminUserId.HasValue)
            {
                assignee = await _context.AdminUsers.Include(x => x.PagePermissions).FirstOrDefaultAsync(x => x.Id == assignedAdminUserId.Value && x.IsActive, ct);
                if (assignee is null || !(assignee.IsEffectiveSuperAdmin || assignee.PagePermissions.Any(x => x.Page == AdminPage.MessageInbox)))
                    return ApiResponse<MetaInboxConversationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "The selected user does not have Message Inbox access.");
            }
            conversation.AssignedAdminUserId = assignee?.Id;
            conversation.AssignedAdminUser = assignee;
            conversation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            await WriteAuditSafelyAsync(actorAdminUserId, "META_INBOX_ASSIGNED", conversationId, assignee?.Id.ToString() ?? "unassigned", ct);
            return ApiResponse<MetaInboxConversationDto>.SuccessResponse(MapConversation(conversation));
        }

        public async Task<ApiResponse<MetaInboxConversationDto>> UpdateStatusAsync(int conversationId, string status, int actorAdminUserId, CancellationToken ct = default)
        {
            var normalizedStatus = NormalizeStatus(status, allowAll: false);
            if (normalizedStatus is null)
                return ApiResponse<MetaInboxConversationDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Status must be open or closed.");
            var conversation = await FindConversationAsync(conversationId, ct);
            if (conversation is null) return NotFound<MetaInboxConversationDto>();
            conversation.Status = normalizedStatus;
            conversation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            await WriteAuditSafelyAsync(actorAdminUserId, "META_INBOX_STATUS_UPDATED", conversationId, normalizedStatus, ct);
            return ApiResponse<MetaInboxConversationDto>.SuccessResponse(MapConversation(conversation));
        }

        public async Task<ApiResponse<MetaInboxConversationDto>> MarkReadAsync(int conversationId, CancellationToken ct = default)
        {
            var conversation = await FindConversationAsync(conversationId, ct);
            if (conversation is null) return NotFound<MetaInboxConversationDto>();
            if (conversation.UnreadCount > 0)
            {
                conversation.UnreadCount = 0;
                conversation.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }
            return ApiResponse<MetaInboxConversationDto>.SuccessResponse(MapConversation(conversation));
        }

        public async Task<ApiResponse<MetaInboxMessageDto>> SendMessageAsync(int conversationId, string text, int actorAdminUserId, CancellationToken ct = default)
        {
            var normalizedText = (text ?? string.Empty).Trim();
            if (normalizedText.Length is < 1 or > 2000)
                return ApiResponse<MetaInboxMessageDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Message text must contain between 1 and 2000 characters.");

            var conversation = await FindConversationAsync(conversationId, ct);
            if (conversation is null) return NotFound<MetaInboxMessageDto>();
            if (!IsReadyForChannel(conversation.Channel))
                return ApiResponse<MetaInboxMessageDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, $"{ChannelName(conversation.Channel)} is not configured on the server.");

            if (conversation.Channel == MetaInboxChannel.WhatsApp)
            {
                if (!string.Equals(conversation.AccountExternalId, _options.WhatsAppPhoneNumberId, StringComparison.Ordinal))
                    return ApiResponse<MetaInboxMessageDto>.ErrorResponse(
                        ErrorCode.VALIDATION_ERROR,
                        "This conversation belongs to a different WhatsApp phone number and cannot be sent with the configured account.");

                var lastIncomingAt = await _context.MetaInboxMessages.AsNoTracking()
                    .Where(x => x.ConversationId == conversationId && x.Direction == MetaInboxMessageDirection.Incoming)
                    .MaxAsync(x => (DateTime?)x.CreatedAt, ct);
                if (!lastIncomingAt.HasValue || lastIncomingAt.Value < DateTime.UtcNow.AddHours(-24))
                    return ApiResponse<MetaInboxMessageDto>.ErrorResponse(
                        "WHATSAPP_WINDOW_CLOSED",
                        "WhatsApp's 24-hour customer service window is closed. Send an approved template before sending a free-form reply.");
            }

            var endpoint = $"{NormalizeVersion()}/{Uri.EscapeDataString(conversation.AccountExternalId)}/messages";
            var content = conversation.Channel switch
            {
                MetaInboxChannel.Instagram => JsonContent.Create(new
                {
                    recipient = new { id = conversation.ParticipantExternalId },
                    message = new { text = normalizedText }
                }),
                MetaInboxChannel.WhatsApp => JsonContent.Create(new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = conversation.ParticipantExternalId,
                    type = "text",
                    text = new { preview_url = false, body = normalizedText }
                }),
                _ => JsonContent.Create(new
                {
                    recipient = new { id = conversation.ParticipantExternalId },
                    messaging_type = "RESPONSE",
                    message = new { text = normalizedText }
                })
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                conversation.Channel == MetaInboxChannel.WhatsApp ? _options.WhatsAppAccessToken : _options.PageAccessToken);
            using var response = await _client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return ApiResponse<MetaInboxMessageDto>.ErrorResponse("META_SEND_FAILED", $"Meta rejected the message with HTTP {(int)response.StatusCode}.");

            string? externalMessageId = null;
            try
            {
                using var responseDocument = JsonDocument.Parse(responseBody);
                var responseRoot = responseDocument.RootElement;
                externalMessageId = ReadString(responseRoot, "message_id");
                if (string.IsNullOrWhiteSpace(externalMessageId) &&
                    responseRoot.TryGetProperty("messages", out var sentMessages) && sentMessages.ValueKind == JsonValueKind.Array)
                    externalMessageId = sentMessages.EnumerateArray().Select(x => ReadString(x, "id")).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            }
            catch (JsonException) { }

            if (!string.IsNullOrWhiteSpace(externalMessageId))
            {
                var existingMessage = await _context.MetaInboxMessages.Include(x => x.SentByAdminUser)
                    .FirstOrDefaultAsync(x => x.ConversationId == conversationId && x.ExternalMessageId == externalMessageId, ct);
                if (existingMessage is not null)
                {
                    existingMessage.SentByAdminUserId ??= actorAdminUserId;
                    await _context.SaveChangesAsync(ct);
                    return ApiResponse<MetaInboxMessageDto>.SuccessResponse(MapMessage(existingMessage));
                }
            }

            var message = new MetaInboxMessage
            {
                ConversationId = conversation.Id,
                ExternalMessageId = externalMessageId,
                Direction = MetaInboxMessageDirection.Outgoing,
                Text = normalizedText,
                AttachmentsJson = "[]",
                SentByAdminUserId = actorAdminUserId,
                DeliveryStatus = "sent",
                CreatedAt = DateTime.UtcNow
            };
            _context.MetaInboxMessages.Add(message);
            conversation.LastMessageAt = message.CreatedAt;
            conversation.LastMessagePreview = normalizedText.Length <= 500 ? normalizedText : normalizedText[..499] + "…";
            conversation.UpdatedAt = message.CreatedAt;
            await _context.SaveChangesAsync(ct);
            message.SentByAdminUser = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorAdminUserId, ct);
            await WriteAuditSafelyAsync(actorAdminUserId, "META_INBOX_MESSAGE_SENT", conversationId, null, ct);
            return ApiResponse<MetaInboxMessageDto>.SuccessResponse(MapMessage(message));
        }

        public async Task<ApiResponse<MetaInboxInternalNoteDto>> AddNoteAsync(int conversationId, string body, int actorAdminUserId, CancellationToken ct = default)
        {
            var normalizedBody = (body ?? string.Empty).Trim();
            if (normalizedBody.Length is < 1 or > 2000)
                return ApiResponse<MetaInboxInternalNoteDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Internal note must contain between 1 and 2000 characters.");
            if (!await _context.MetaInboxConversations.AnyAsync(x => x.Id == conversationId, ct))
                return NotFound<MetaInboxInternalNoteDto>();
            var note = new MetaInboxInternalNote
            {
                ConversationId = conversationId,
                AuthorAdminUserId = actorAdminUserId,
                Body = normalizedBody,
                CreatedAt = DateTime.UtcNow
            };
            _context.MetaInboxInternalNotes.Add(note);
            await _context.SaveChangesAsync(ct);
            note.AuthorAdminUser = await _context.AdminUsers.AsNoTracking().FirstAsync(x => x.Id == actorAdminUserId, ct);
            await WriteAuditSafelyAsync(actorAdminUserId, "META_INBOX_INTERNAL_NOTE_ADDED", conversationId, null, ct);
            return ApiResponse<MetaInboxInternalNoteDto>.SuccessResponse(MapNote(note));
        }

        private async Task<MetaInboxConversation?> FindConversationAsync(int id, CancellationToken ct)
            => await _context.MetaInboxConversations.Include(x => x.AssignedAdminUser).FirstOrDefaultAsync(x => x.Id == id, ct);

        private async Task TryPopulateProfileAsync(MetaInboxConversation conversation, CancellationToken ct)
        {
            if (conversation.Channel == MetaInboxChannel.WhatsApp || string.IsNullOrWhiteSpace(_options.PageAccessToken)) return;
            try
            {
                var fields = conversation.Channel == MetaInboxChannel.Instagram ? "name,username,profile_pic" : "name,first_name,last_name,profile_pic";
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{NormalizeVersion()}/{Uri.EscapeDataString(conversation.ParticipantExternalId)}?fields={Uri.EscapeDataString(fields)}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.PageAccessToken);
                using var response = await _client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) return;
                using var profile = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var root = profile.RootElement;
                var name = ReadString(root, "name") ?? ReadString(root, "username");
                if (!string.IsNullOrWhiteSpace(name)) conversation.ParticipantDisplayName = Trim(name, 200);
                var avatar = ReadString(root, "profile_pic");
                if (Uri.TryCreate(avatar, UriKind.Absolute, out var avatarUri) && avatarUri.Scheme == Uri.UriSchemeHttps)
                    conversation.ParticipantAvatarUrl = Trim(avatar, 2048);
                await _context.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (HttpRequestException) { }
            catch (JsonException) { }
        }

        private async Task WriteAuditSafelyAsync(int actorAdminUserId, string action, int conversationId, string? summary, CancellationToken ct)
        {
            try { await _audit.WriteAsync(actorAdminUserId, null, action, "MetaInboxConversation", conversationId.ToString(), summary, true, ct); }
            catch { }
        }

        private bool IsReadyForChannel(MetaInboxChannel channel)
        {
            var commonReady = _options.Enabled && !string.IsNullOrWhiteSpace(_options.AppSecret) && !string.IsNullOrWhiteSpace(_options.VerifyToken);
            if (!commonReady) return false;
            return channel == MetaInboxChannel.WhatsApp
                ? !string.IsNullOrWhiteSpace(_options.WhatsAppAccessToken) &&
                  !string.IsNullOrWhiteSpace(_options.WhatsAppPhoneNumberId)
                : !string.IsNullOrWhiteSpace(_options.PageAccessToken);
        }

        private string NormalizeVersion() => string.IsNullOrWhiteSpace(_options.GraphApiVersion) ? "v26.0" : _options.GraphApiVersion.Trim().Trim('/');

        private static string ChannelName(MetaInboxChannel channel) => channel switch
        {
            MetaInboxChannel.Instagram => "Instagram",
            MetaInboxChannel.WhatsApp => "WhatsApp",
            _ => "Messenger"
        };

        private static MetaInboxConversationDto MapConversation(MetaInboxConversation x) => new(
            x.Id,
            x.Channel switch
            {
                MetaInboxChannel.Instagram => "instagram",
                MetaInboxChannel.WhatsApp => "whatsapp",
                _ => "messenger"
            },
            x.ParticipantExternalId,
            string.IsNullOrWhiteSpace(x.ParticipantDisplayName) ? x.ParticipantExternalId : x.ParticipantDisplayName,
            x.ParticipantAvatarUrl,
            x.AssignedAdminUserId,
            x.AssignedAdminUser is null ? null : (string.IsNullOrWhiteSpace(x.AssignedAdminUser.DisplayName) ? x.AssignedAdminUser.Username : x.AssignedAdminUser.DisplayName),
            x.Status,
            x.UnreadCount,
            x.LastMessagePreview,
            x.LastMessageAt);

        private static MetaInboxMessageDto MapMessage(MetaInboxMessage x) => new(
            x.Id,
            x.Direction == MetaInboxMessageDirection.Incoming ? "incoming" : "outgoing",
            x.Text,
            ParseAttachments(x.AttachmentsJson),
            x.SentByAdminUserId,
            x.SentByAdminUser is null ? null : (string.IsNullOrWhiteSpace(x.SentByAdminUser.DisplayName) ? x.SentByAdminUser.Username : x.SentByAdminUser.DisplayName),
            x.DeliveryStatus,
            x.CreatedAt);

        private static MetaInboxInternalNoteDto MapNote(MetaInboxInternalNote x) => new(
            x.Id,
            x.AuthorAdminUserId,
            string.IsNullOrWhiteSpace(x.AuthorAdminUser?.DisplayName) ? x.AuthorAdminUser?.Username ?? "Admin" : x.AuthorAdminUser.DisplayName,
            x.Body,
            x.CreatedAt);

        private static IReadOnlyDictionary<string, string> ReadWhatsAppContactNames(JsonElement value)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!value.TryGetProperty("contacts", out var contacts) || contacts.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var contact in contacts.EnumerateArray().Take(50))
            {
                var whatsappId = ReadString(contact, "wa_id");
                var name = contact.TryGetProperty("profile", out var profile) ? ReadString(profile, "name") : null;
                if (!string.IsNullOrWhiteSpace(whatsappId) && !string.IsNullOrWhiteSpace(name))
                    result[whatsappId] = Trim(name, 200);
            }
            return result;
        }

        private static string? ReadWhatsAppText(JsonElement message, string messageType)
        {
            if (messageType == "text" && message.TryGetProperty("text", out var text))
                return ReadString(text, "body");
            if (messageType == "button" && message.TryGetProperty("button", out var button))
                return ReadString(button, "text") ?? ReadString(button, "payload");
            if (messageType == "reaction" && message.TryGetProperty("reaction", out var reaction))
                return ReadString(reaction, "emoji") is { Length: > 0 } emoji ? $"Reaction: {emoji}" : "Reaction removed";
            if (messageType == "interactive" && message.TryGetProperty("interactive", out var interactive))
            {
                if (interactive.TryGetProperty("button_reply", out var buttonReply))
                    return ReadString(buttonReply, "title") ?? ReadString(buttonReply, "id");
                if (interactive.TryGetProperty("list_reply", out var listReply))
                    return ReadString(listReply, "title") ?? ReadString(listReply, "id");
            }
            if (messageType == "location" && message.TryGetProperty("location", out var location) &&
                location.TryGetProperty("latitude", out var latitude) && latitude.TryGetDouble(out var lat) &&
                location.TryGetProperty("longitude", out var longitude) && longitude.TryGetDouble(out var lng))
                return $"Location: {lat:F6}, {lng:F6}";
            return messageType switch
            {
                "contacts" => "[contact]",
                "order" => "[order]",
                "system" => "[system message]",
                "unsupported" => "[unsupported message]",
                _ => null
            };
        }

        private static List<StoredAttachment> ReadWhatsAppAttachments(JsonElement message, string messageType)
        {
            if (messageType is not ("image" or "video" or "audio" or "document" or "sticker"))
                return new List<StoredAttachment>();
            if (!message.TryGetProperty(messageType, out var media) || media.ValueKind != JsonValueKind.Object)
                return new List<StoredAttachment> { new(messageType, null, null) };

            var title = ReadString(media, "caption") ?? ReadString(media, "filename");
            return new List<StoredAttachment> { new(Trim(messageType, 40), null, TrimOrNull(title, 200)) };
        }

        private static DateTime ReadWhatsAppTimestamp(JsonElement value)
        {
            var raw = ReadString(value, "timestamp");
            if (long.TryParse(raw, out var seconds))
            {
                try { return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime; }
                catch (ArgumentOutOfRangeException) { }
            }
            return DateTime.UtcNow;
        }

        private static int DeliveryStatusRank(string? status) => status?.Trim().ToLowerInvariant() switch
        {
            "received" => 0,
            "sent" => 1,
            "delivered" => 2,
            "read" => 3,
            _ => -1
        };

        private static List<StoredAttachment> ReadAttachments(JsonElement message)
        {
            var result = new List<StoredAttachment>();
            if (!message.TryGetProperty("attachments", out var attachments) || attachments.ValueKind != JsonValueKind.Array) return result;
            foreach (var attachment in attachments.EnumerateArray().Take(10))
            {
                var type = ReadString(attachment, "type") ?? "file";
                var title = ReadString(attachment, "title");
                string? url = null;
                if (attachment.TryGetProperty("payload", out var attachmentPayload)) url = ReadString(attachmentPayload, "url");
                if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)) url = null;
                result.Add(new StoredAttachment(Trim(type, 40), TrimOrNull(url, 2048), TrimOrNull(title, 200)));
            }
            return result;
        }

        private static IReadOnlyList<MetaInboxAttachmentDto> ParseAttachments(string? json)
        {
            try
            {
                return (JsonSerializer.Deserialize<List<StoredAttachment>>(json ?? "[]") ?? new List<StoredAttachment>())
                    .Select(x => new MetaInboxAttachmentDto(x.Type, x.Url, x.Title)).ToList();
            }
            catch (JsonException) { return Array.Empty<MetaInboxAttachmentDto>(); }
        }

        private static string BuildPreview(string? text, IReadOnlyList<StoredAttachment> attachments)
        {
            var value = string.IsNullOrWhiteSpace(text) ? (attachments.Count > 0 ? $"[{attachments[0].Type}]" : "[message]") : text.Trim();
            return value.Length <= 500 ? value : value[..499] + "…";
        }

        private static string? NormalizeStatus(string? value, bool allowAll)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            if (allowAll && (string.IsNullOrWhiteSpace(normalized) || normalized == "all")) return null;
            return normalized is "open" or "closed" ? normalized : null;
        }

        private static DateTime ReadTimestamp(JsonElement value)
        {
            if (value.TryGetProperty("timestamp", out var timestamp) && timestamp.TryGetInt64(out var milliseconds))
            {
                try { return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime; }
                catch (ArgumentOutOfRangeException) { }
            }
            return DateTime.UtcNow;
        }

        private static DateTime? ReadUnixMilliseconds(JsonElement value, string property)
        {
            if (!value.TryGetProperty(property, out var timestamp) || !timestamp.TryGetInt64(out var milliseconds)) return null;
            try { return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime; }
            catch (ArgumentOutOfRangeException) { return null; }
        }

        private static string? ReadString(JsonElement value, string property)
            => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
        private static string Trim(string? value, int max) => (value ?? string.Empty).Trim() is var trimmed && trimmed.Length <= max ? trimmed : trimmed[..max];
        private static string? TrimOrNull(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : Trim(value, max);
        private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException is SqlException { Number: 2601 or 2627 };
        private static ApiResponse<T> NotFound<T>() => ApiResponse<T>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Conversation was not found.");
        private sealed record StoredAttachment(string Type, string? Url, string? Title);
    }
}
