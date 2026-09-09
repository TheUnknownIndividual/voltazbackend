#nullable enable

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
        private readonly IMetaInboxHistorySyncService _historySync;
        private readonly IFileService _fileService;

        public MetaInboxService(
            DataContext context,
            HttpClient client,
            IOptions<MetaInboxOptions> options,
            IAdminAuditService audit,
            IMetaInboxHistorySyncService historySync,
            IFileService fileService)
        {
            _context = context;
            _client = client;
            _options = options.Value;
            _audit = audit;
            _historySync = historySync;
            _fileService = fileService;
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
                        if (value.TryGetProperty("message_echoes", out var echoes) && echoes.ValueKind == JsonValueKind.Array)
                            foreach (var _ in echoes.EnumerateArray()) messageCount++;
                        if (value.TryGetProperty("history", out var history) && history.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var historyItem in history.EnumerateArray())
                            {
                                if (!historyItem.TryGetProperty("threads", out var threads) || threads.ValueKind != JsonValueKind.Array) continue;
                                foreach (var thread in threads.EnumerateArray())
                                {
                                    if (!thread.TryGetProperty("messages", out var historicalMessages) || historicalMessages.ValueKind != JsonValueKind.Array) continue;
                                    foreach (var _ in historicalMessages.EnumerateArray()) messageCount++;
                                }
                            }
                        }
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
                    var field = ReadString(change, "field");
                    if (string.IsNullOrWhiteSpace(field) ||
                        !change.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object)
                        continue;

                    var phoneNumberId = value.TryGetProperty("metadata", out var metadata)
                        ? ReadString(metadata, "phone_number_id")
                        : null;
                    if (string.IsNullOrWhiteSpace(phoneNumberId) ||
                        (!string.IsNullOrWhiteSpace(_options.WhatsAppPhoneNumberId) &&
                         !string.Equals(phoneNumberId, _options.WhatsAppPhoneNumberId, StringComparison.Ordinal)))
                        continue;

                    if (field == "messages")
                    {
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
                    else if (field == "history")
                    {
                        await ProcessWhatsAppHistoryAsync(phoneNumberId, value, ct);
                    }
                    else if (field == "smb_message_echoes")
                    {
                        await ProcessWhatsAppMessageEchoesAsync(phoneNumberId, value, ct);
                    }
                    else if (field == "smb_app_state_sync")
                    {
                        await ProcessWhatsAppContactStateSyncAsync(phoneNumberId, value, ct);
                    }
                }
            }
        }

        private async Task ProcessWhatsAppHistoryAsync(string phoneNumberId, JsonElement value, CancellationToken ct)
        {
            if (!value.TryGetProperty("history", out var historyItems) || historyItems.ValueKind != JsonValueKind.Array)
                return;

            foreach (var historyItem in historyItems.EnumerateArray())
            {
                if (historyItem.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    var firstError = errors.EnumerateArray().FirstOrDefault();
                    var code = ReadInt(firstError, "code")?.ToString() ?? "META_HISTORY_SYNC_FAILED";
                    var declined = code == "2593109";
                    await _historySync.RecordFailureAsync(new MetaInboxHistorySyncFailureUpdate(
                        phoneNumberId,
                        declined ? MetaInboxHistorySyncStatuses.Declined : MetaInboxHistorySyncStatuses.Failed,
                        code,
                        declined
                            ? "Chat history sharing was declined in the WhatsApp Business app."
                            : "Meta reported that chat history synchronization failed.",
                        DateTime.UtcNow), ct);
                    continue;
                }

                var phase = historyItem.TryGetProperty("metadata", out var historyMetadata)
                    ? ReadInt(historyMetadata, "phase")
                    : null;
                var chunkOrder = historyItem.TryGetProperty("metadata", out historyMetadata)
                    ? ReadInt(historyMetadata, "chunk_order")
                    : null;
                var progress = historyItem.TryGetProperty("metadata", out historyMetadata)
                    ? ReadInt(historyMetadata, "progress") ?? 0
                    : 0;

                if (historyItem.TryGetProperty("threads", out var threads) && threads.ValueKind == JsonValueKind.Array)
                {
                    foreach (var thread in threads.EnumerateArray())
                        await StoreWhatsAppHistoryThreadAsync(phoneNumberId, thread, ct);
                }

                await _historySync.RecordProgressAsync(new MetaInboxHistorySyncProgressUpdate(
                    phoneNumberId,
                    Math.Clamp(progress, 0, 100),
                    phase,
                    chunkOrder,
                    DateTime.UtcNow), ct);
            }
        }

        private async Task ProcessWhatsAppMessageEchoesAsync(string phoneNumberId, JsonElement value, CancellationToken ct)
        {
            if (!value.TryGetProperty("message_echoes", out var echoes) || echoes.ValueKind != JsonValueKind.Array)
                return;

            foreach (var echo in echoes.EnumerateArray())
            {
                var participantId = NormalizeWhatsAppParticipant(ReadString(echo, "to") ?? ReadString(echo, "recipient_id"));
                if (string.IsNullOrWhiteSpace(participantId)) continue;
                await StoreWhatsAppMessageAsync(
                    phoneNumberId,
                    echo,
                    new Dictionary<string, string>(),
                    ct,
                    MetaInboxMessageDirection.Outgoing,
                    participantId);
            }
        }

        private async Task ProcessWhatsAppContactStateSyncAsync(string phoneNumberId, JsonElement value, CancellationToken ct)
        {
            if (!value.TryGetProperty("state_sync", out var stateItems) || stateItems.ValueKind != JsonValueKind.Array)
                return;

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var stateItem in stateItems.EnumerateArray())
            {
                if (!string.Equals(ReadString(stateItem, "type"), "contact", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ReadString(stateItem, "action"), "add", StringComparison.OrdinalIgnoreCase) ||
                    !stateItem.TryGetProperty("contact", out var contact) || contact.ValueKind != JsonValueKind.Object)
                    continue;

                var participantId = NormalizeWhatsAppParticipant(ReadString(contact, "phone_number"));
                var displayName = ReadString(contact, "full_name") ?? ReadString(contact, "first_name");
                if (string.IsNullOrWhiteSpace(participantId) || string.IsNullOrWhiteSpace(displayName)) continue;
                names[participantId] = Trim(displayName, 200);
            }

            var participantIds = names.Keys.ToList();
            for (var offset = 0; offset < participantIds.Count; offset += 500)
            {
                var batch = participantIds.Skip(offset).Take(500).ToList();
                var conversations = await _context.MetaInboxConversations
                    .Where(x => x.Channel == MetaInboxChannel.WhatsApp &&
                                x.AccountExternalId == phoneNumberId &&
                                batch.Contains(x.ParticipantExternalId))
                    .ToListAsync(ct);
                var now = DateTime.UtcNow;
                foreach (var conversation in conversations)
                {
                    conversation.ParticipantDisplayName = names[conversation.ParticipantExternalId];
                    conversation.UpdatedAt = now;
                }
                if (conversations.Count > 0) await _context.SaveChangesAsync(ct);
            }
        }

        private async Task StoreWhatsAppMessageAsync(
            string phoneNumberId,
            JsonElement incoming,
            IReadOnlyDictionary<string, string> contactNames,
            CancellationToken ct,
            MetaInboxMessageDirection direction = MetaInboxMessageDirection.Incoming,
            string? participantIdOverride = null)
        {
            var participantId = participantIdOverride ?? ReadString(incoming, "from");
            if (string.IsNullOrWhiteSpace(participantId)) return;

            var externalMessageId = ReadString(incoming, "id");
            var messageType = ReadString(incoming, "type") ?? "message";
            var text = ReadWhatsAppText(incoming, messageType);
            var attachments = await ReadWhatsAppAttachmentsAsync(incoming, messageType, ct);
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
        }

        private async Task StoreWhatsAppHistoryThreadAsync(string phoneNumberId, JsonElement thread, CancellationToken ct)
        {
            var participantId = NormalizeWhatsAppParticipant(ReadString(thread, "id"));
            if (string.IsNullOrWhiteSpace(participantId) ||
                !thread.TryGetProperty("messages", out var messagesElement) || messagesElement.ValueKind != JsonValueKind.Array)
                return;

            var drafts = new List<HistoryMessageDraft>();
            foreach (var historyMessage in messagesElement.EnumerateArray())
            {
                var from = NormalizeWhatsAppParticipant(ReadString(historyMessage, "from"));
                var direction = string.Equals(from, participantId, StringComparison.Ordinal)
                    ? MetaInboxMessageDirection.Incoming
                    : MetaInboxMessageDirection.Outgoing;
                var messageType = ReadString(historyMessage, "type") ?? "message";
                var text = ReadWhatsAppText(historyMessage, messageType);
                var attachments = await ReadWhatsAppAttachmentsAsync(historyMessage, messageType, ct);
                var occurredAt = ReadWhatsAppTimestamp(historyMessage);
                var externalMessageId = ReadString(historyMessage, "id");
                if (string.IsNullOrWhiteSpace(externalMessageId))
                    externalMessageId = BuildHistoryFingerprint(phoneNumberId, participantId, from, occurredAt, messageType, text, attachments);
                var historyStatus = historyMessage.TryGetProperty("history_context", out var historyContext)
                    ? ReadString(historyContext, "status")
                    : null;
                drafts.Add(new HistoryMessageDraft(
                    Trim(externalMessageId, 256),
                    direction,
                    TrimOrNull(text, 4000),
                    attachments,
                    NormalizeHistoryDeliveryStatus(historyStatus, direction),
                    occurredAt));
            }

            drafts = drafts
                .GroupBy(x => x.ExternalMessageId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(x => x.OccurredAt)
                .ToList();
            if (drafts.Count == 0) return;

            var conversation = await _context.MetaInboxConversations.FirstOrDefaultAsync(
                x => x.Channel == MetaInboxChannel.WhatsApp && x.AccountExternalId == phoneNumberId &&
                     x.ParticipantExternalId == participantId, ct);
            if (conversation is null)
            {
                var first = drafts[0];
                var last = drafts[^1];
                conversation = new MetaInboxConversation
                {
                    Channel = MetaInboxChannel.WhatsApp,
                    AccountExternalId = phoneNumberId,
                    ParticipantExternalId = participantId,
                    ParticipantDisplayName = participantId,
                    Status = "open",
                    UnreadCount = 0,
                    LastMessagePreview = BuildPreview(last.Text, last.Attachments),
                    LastMessageAt = last.OccurredAt,
                    CreatedAt = first.OccurredAt,
                    UpdatedAt = DateTime.UtcNow
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
                }
            }

            var candidateIds = drafts.Select(x => x.ExternalMessageId).ToList();
            var existingIds = new HashSet<string>(StringComparer.Ordinal);
            for (var offset = 0; offset < candidateIds.Count; offset += 500)
            {
                var batch = candidateIds.Skip(offset).Take(500).ToList();
                var found = await _context.MetaInboxMessages.AsNoTracking()
                    .Where(x => x.ConversationId == conversation.Id &&
                                x.ExternalMessageId != null && batch.Contains(x.ExternalMessageId))
                    .Select(x => x.ExternalMessageId!)
                    .ToListAsync(ct);
                existingIds.UnionWith(found);
            }

            foreach (var draft in drafts.Where(x => !existingIds.Contains(x.ExternalMessageId)))
            {
                _context.MetaInboxMessages.Add(new MetaInboxMessage
                {
                    ConversationId = conversation.Id,
                    ExternalMessageId = draft.ExternalMessageId,
                    Direction = draft.Direction,
                    Text = draft.Text,
                    AttachmentsJson = JsonSerializer.Serialize(draft.Attachments),
                    DeliveryStatus = draft.DeliveryStatus,
                    IsHistorical = true,
                    CreatedAt = draft.OccurredAt
                });
            }

            var latest = drafts[^1];
            if (latest.OccurredAt > conversation.LastMessageAt)
            {
                conversation.LastMessageAt = latest.OccurredAt;
                conversation.LastMessagePreview = BuildPreview(latest.Text, latest.Attachments);
            }
            conversation.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception))
            {
                foreach (var entry in _context.ChangeTracker.Entries<MetaInboxMessage>()
                             .Where(x => x.State == EntityState.Added && x.Entity.ConversationId == conversation.Id))
                    entry.State = EntityState.Detached;
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

        public async Task<bool> CanViewAllConversationsAsync(int actorAdminUserId, CancellationToken ct = default)
            => (await GetConversationAccessAsync(actorAdminUserId, ct)).CanViewAll;

        public async Task<ApiResponse<MetaInboxConversationPageDto>> GetConversationsAsync(string? search, string? status, string? assignment, int actorAdminUserId, int page, int pageSize, CancellationToken ct = default)
        {
            var access = await GetConversationAccessAsync(actorAdminUserId, ct);
            IQueryable<MetaInboxConversation> query = _context.MetaInboxConversations.AsNoTracking()
                .Include(x => x.AssignedAdminUser);
            query = ApplyConversationAccess(query, actorAdminUserId, access);
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

        public async Task<ApiResponse<IReadOnlyList<MetaInboxMessageDto>>> GetMessagesAsync(int conversationId, long? afterId, int actorAdminUserId, CancellationToken ct = default)
        {
            if (!await CanAccessConversationAsync(conversationId, actorAdminUserId, ct))
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

        public async Task<ApiResponse<IReadOnlyList<MetaInboxInternalNoteDto>>> GetNotesAsync(int conversationId, long? afterId, int actorAdminUserId, CancellationToken ct = default)
        {
            if (!await CanAccessConversationAsync(conversationId, actorAdminUserId, ct))
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

        public async Task<ApiResponse<IReadOnlyList<MetaInboxAssigneeDto>>> GetAssigneesAsync(int actorAdminUserId, CancellationToken ct = default)
        {
            if (!(await GetConversationAccessAsync(actorAdminUserId, ct)).Exists)
                return ApiResponse<IReadOnlyList<MetaInboxAssigneeDto>>.ErrorResponse(
                    ErrorCode.VALIDATION_ERROR,
                    "Admin session was not found.");

            var admins = await _context.AdminUsers.AsNoTracking()
                .Where(x => x.IsActive && (x.IsSuperAdmin || x.Username == "admin" || x.PagePermissions.Any(p => p.Page == AdminPage.MessageInbox)))
                .OrderBy(x => x.DisplayName).ThenBy(x => x.Username).ToListAsync(ct);
            return ApiResponse<IReadOnlyList<MetaInboxAssigneeDto>>.SuccessResponse(admins
                .Select(x => new MetaInboxAssigneeDto(x.Id, string.IsNullOrWhiteSpace(x.DisplayName) ? x.Username : x.DisplayName)).ToList());
        }

        public async Task<ApiResponse<int>> GetUnreadCountAsync(int actorAdminUserId, CancellationToken ct = default)
        {
            var access = await GetConversationAccessAsync(actorAdminUserId, ct);
            var query = ApplyConversationAccess(
                _context.MetaInboxConversations.AsNoTracking(),
                actorAdminUserId,
                access);
            return ApiResponse<int>.SuccessResponse(await query.SumAsync(x => (int?)x.UnreadCount, ct) ?? 0);
        }

        public async Task<ApiResponse<MetaInboxConversationDto>> AssignAsync(int conversationId, int? assignedAdminUserId, int actorAdminUserId, CancellationToken ct = default)
        {
            var conversation = await FindConversationAsync(conversationId, actorAdminUserId, ct);
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
            var conversation = await FindConversationAsync(conversationId, actorAdminUserId, ct);
            if (conversation is null) return NotFound<MetaInboxConversationDto>();
            conversation.Status = normalizedStatus;
            conversation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            await WriteAuditSafelyAsync(actorAdminUserId, "META_INBOX_STATUS_UPDATED", conversationId, normalizedStatus, ct);
            return ApiResponse<MetaInboxConversationDto>.SuccessResponse(MapConversation(conversation));
        }

        public async Task<ApiResponse<MetaInboxConversationDto>> MarkReadAsync(int conversationId, int actorAdminUserId, CancellationToken ct = default)
        {
            var conversation = await FindConversationAsync(conversationId, actorAdminUserId, ct);
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

            var conversation = await FindConversationAsync(conversationId, actorAdminUserId, ct);
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

        public async Task<ApiResponse<MetaInboxMessageDto>> SendAttachmentAsync(int conversationId, FileUploadRequest file, string? caption, int actorAdminUserId, CancellationToken ct = default)
        {
            var normalizedCaption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
            if (normalizedCaption is { Length: > 2000 })
                return ApiResponse<MetaInboxMessageDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Caption must be 2000 characters or fewer.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                return ApiResponse<MetaInboxMessageDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Only JPG, PNG, or WEBP images can be sent.");

            var conversation = await FindConversationAsync(conversationId, actorAdminUserId, ct);
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

            string uploadedUrl;
            try { uploadedUrl = await _fileService.UploadImageAsync(file, "meta-inbox", ct); }
            catch (Exception) { return ApiResponse<MetaInboxMessageDto>.ErrorResponse("UPLOAD_FAILED", "The image could not be uploaded."); }

            var endpoint = $"{NormalizeVersion()}/{Uri.EscapeDataString(conversation.AccountExternalId)}/messages";
            var content = conversation.Channel switch
            {
                MetaInboxChannel.Instagram => JsonContent.Create(new
                {
                    recipient = new { id = conversation.ParticipantExternalId },
                    message = new { attachment = new { type = "image", payload = new { url = uploadedUrl, is_reusable = true } } }
                }),
                MetaInboxChannel.WhatsApp => JsonContent.Create(new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = conversation.ParticipantExternalId,
                    type = "image",
                    image = new { link = uploadedUrl, caption = normalizedCaption }
                }),
                _ => JsonContent.Create(new
                {
                    recipient = new { id = conversation.ParticipantExternalId },
                    messaging_type = "RESPONSE",
                    message = new { attachment = new { type = "image", payload = new { url = uploadedUrl, is_reusable = true } } }
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

            var attachments = new List<StoredAttachment> { new("image", uploadedUrl, normalizedCaption) };
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
                Text = normalizedCaption,
                AttachmentsJson = JsonSerializer.Serialize(attachments),
                SentByAdminUserId = actorAdminUserId,
                DeliveryStatus = "sent",
                CreatedAt = DateTime.UtcNow
            };
            _context.MetaInboxMessages.Add(message);
            conversation.LastMessageAt = message.CreatedAt;
            conversation.LastMessagePreview = BuildPreview(normalizedCaption, attachments);
            conversation.UpdatedAt = message.CreatedAt;
            await _context.SaveChangesAsync(ct);
            message.SentByAdminUser = await _context.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actorAdminUserId, ct);
            await WriteAuditSafelyAsync(actorAdminUserId, "META_INBOX_ATTACHMENT_SENT", conversationId, null, ct);
            return ApiResponse<MetaInboxMessageDto>.SuccessResponse(MapMessage(message));
        }

        public async Task<ApiResponse<MetaInboxInternalNoteDto>> AddNoteAsync(int conversationId, string body, int actorAdminUserId, CancellationToken ct = default)
        {
            var normalizedBody = (body ?? string.Empty).Trim();
            if (normalizedBody.Length is < 1 or > 2000)
                return ApiResponse<MetaInboxInternalNoteDto>.ErrorResponse(ErrorCode.VALIDATION_ERROR, "Internal note must contain between 1 and 2000 characters.");
            if (!await CanAccessConversationAsync(conversationId, actorAdminUserId, ct))
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

        private async Task<MetaInboxConversation?> FindConversationAsync(int id, int actorAdminUserId, CancellationToken ct)
        {
            var access = await GetConversationAccessAsync(actorAdminUserId, ct);
            IQueryable<MetaInboxConversation> query = _context.MetaInboxConversations.Include(x => x.AssignedAdminUser);
            query = ApplyConversationAccess(query, actorAdminUserId, access);
            return await query.FirstOrDefaultAsync(x => x.Id == id, ct);
        }

        private async Task<bool> CanAccessConversationAsync(int conversationId, int actorAdminUserId, CancellationToken ct)
        {
            var access = await GetConversationAccessAsync(actorAdminUserId, ct);
            var query = ApplyConversationAccess(
                _context.MetaInboxConversations.AsNoTracking(),
                actorAdminUserId,
                access);
            return await query.AnyAsync(x => x.Id == conversationId, ct);
        }

        private async Task<ConversationAccess> GetConversationAccessAsync(int actorAdminUserId, CancellationToken ct)
        {
            var actor = await _context.AdminUsers.AsNoTracking()
                .Where(x => x.Id == actorAdminUserId && x.IsActive)
                .Select(x => new { x.Username, x.IsSuperAdmin, x.IsStakeholder })
                .FirstOrDefaultAsync(ct);
            if (actor is null) return new ConversationAccess(false, false);

            var canViewAll = actor.IsSuperAdmin ||
                             actor.IsStakeholder ||
                             AdminUser.IsPrimaryUsername(actor.Username);
            return new ConversationAccess(true, canViewAll);
        }

        private static IQueryable<MetaInboxConversation> ApplyConversationAccess(
            IQueryable<MetaInboxConversation> query,
            int actorAdminUserId,
            ConversationAccess access)
        {
            if (!access.Exists) return query.Where(_ => false);
            if (access.CanViewAll) return query;
            return query.Where(x => x.AssignedAdminUserId == null || x.AssignedAdminUserId == actorAdminUserId);
        }

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

        private async Task<List<StoredAttachment>> ReadWhatsAppAttachmentsAsync(JsonElement message, string messageType, CancellationToken ct)
        {
            if (messageType == "media_placeholder")
                return new List<StoredAttachment> { new("media", null, "Historical media") };
            if (messageType is not ("image" or "video" or "audio" or "document" or "sticker"))
                return new List<StoredAttachment>();
            if (!message.TryGetProperty(messageType, out var media) || media.ValueKind != JsonValueKind.Object)
                return new List<StoredAttachment> { new(messageType, null, null) };

            var title = TrimOrNull(ReadString(media, "caption") ?? ReadString(media, "filename"), 200);
            var mediaId = ReadString(media, "id");

            if (!string.IsNullOrWhiteSpace(mediaId) && messageType is "image" or "sticker" or "audio" or "video" or "document")
                return new List<StoredAttachment> { await ResolveWhatsAppMediaAttachmentAsync(messageType, mediaId, title, ct) };

            return new List<StoredAttachment> { new(Trim(messageType, 40), null, title) };
        }

        private async Task<StoredAttachment> ResolveWhatsAppMediaAttachmentAsync(string messageType, string mediaId, string? title, CancellationToken ct)
        {
            var fallback = new StoredAttachment(Trim(messageType, 40), null, title);
            if (string.IsNullOrWhiteSpace(_options.WhatsAppAccessToken)) return fallback;
            try
            {
                using var metaRequest = new HttpRequestMessage(HttpMethod.Get, $"{NormalizeVersion()}/{Uri.EscapeDataString(mediaId)}");
                metaRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.WhatsAppAccessToken);
                using var metaResponse = await _client.SendAsync(metaRequest, ct);
                if (!metaResponse.IsSuccessStatusCode) return fallback;

                using var metaDocument = JsonDocument.Parse(await metaResponse.Content.ReadAsStringAsync(ct));
                var mediaUrl = ReadString(metaDocument.RootElement, "url");
                var mimeType = ReadString(metaDocument.RootElement, "mime_type");
                if (string.IsNullOrWhiteSpace(mediaUrl) || string.IsNullOrWhiteSpace(mimeType)) return fallback;

                var extension = mimeType.Split(';')[0].Trim().ToLowerInvariant() switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    "audio/ogg" or "audio/opus" => ".ogg",
                    "audio/mpeg" => ".mp3",
                    "audio/mp4" => ".m4a",
                    "audio/aac" => ".aac",
                    "audio/amr" or "audio/amr-nb" or "audio/amr-wb" => ".amr",
                    "audio/wav" or "audio/x-wav" => ".wav",
                    "video/mp4" => ".mp4",
                    "video/3gpp" => ".3gp",
                    "video/quicktime" => ".mov",
                    "application/pdf" => ".pdf",
                    "application/msword" => ".doc",
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
                    "application/vnd.ms-excel" => ".xls",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
                    "application/vnd.ms-powerpoint" => ".ppt",
                    "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ".pptx",
                    "text/plain" => ".txt",
                    "text/csv" => ".csv",
                    "application/zip" => ".zip",
                    _ => null
                };
                if (extension is null) return fallback;

                using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, mediaUrl);
                downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.WhatsAppAccessToken);
                using var downloadResponse = await _client.SendAsync(downloadRequest, ct);
                if (!downloadResponse.IsSuccessStatusCode) return fallback;

                await using var bytes = await downloadResponse.Content.ReadAsStreamAsync(ct);
                var uploadRequest = new FileUploadRequest { FileName = $"{mediaId}{extension}", Content = bytes };
                var permanentUrl = messageType is "image" or "sticker"
                    ? await _fileService.UploadImageAsync(uploadRequest, "meta-inbox", ct)
                    : await _fileService.UploadMediaAsync(uploadRequest, "meta-inbox", ct);
                return new StoredAttachment(Trim(messageType, 40), TrimOrNull(permanentUrl, 2048), title);
            }
            catch
            {
                return fallback;
            }
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

        private static int? ReadInt(JsonElement value, string property)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(property, out var item)) return null;
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var number)) return number;
            return item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out number) ? number : null;
        }

        private static string? NormalizeWhatsAppParticipant(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var normalized = new string(value.Where(char.IsDigit).ToArray());
            return normalized.Length is > 0 and <= 128 ? normalized : null;
        }

        private static string BuildHistoryFingerprint(
            string phoneNumberId,
            string participantId,
            string? sender,
            DateTime occurredAt,
            string messageType,
            string? text,
            IReadOnlyList<StoredAttachment> attachments)
        {
            var material = string.Join('|',
                phoneNumberId,
                participantId,
                sender ?? string.Empty,
                occurredAt.Ticks.ToString(),
                messageType,
                TrimOrNull(text, 4000) ?? string.Empty,
                attachments.FirstOrDefault()?.Title ?? string.Empty);
            return "history:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
        }

        private static string NormalizeHistoryDeliveryStatus(string? value, MetaInboxMessageDirection direction)
        {
            if (direction == MetaInboxMessageDirection.Incoming) return "received";
            return value?.Trim().ToLowerInvariant() switch
            {
                "read" or "played" => "read",
                "delivered" => "delivered",
                "error" or "failed" => "failed",
                "pending" or "sent" => "sent",
                _ => "sent"
            };
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
        private sealed record ConversationAccess(bool Exists, bool CanViewAll);
        private sealed record StoredAttachment(string Type, string? Url, string? Title);
        private sealed record HistoryMessageDraft(
            string ExternalMessageId,
            MetaInboxMessageDirection Direction,
            string? Text,
            IReadOnlyList<StoredAttachment> Attachments,
            string DeliveryStatus,
            DateTime OccurredAt);
    }
}
