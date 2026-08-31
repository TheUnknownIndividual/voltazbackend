using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Volt.Application.Dtos.AdminProjectTracker;
using Volt.Application.Dtos.SolarAnalytics;
using Microsoft.Extensions.Options;
using Volt.Application.Configuration;
using Volt.Application.Interfaces;

namespace Volt.Infrastructure.Services;

/// <summary>Small, bounded Telegram sender. Assignment persistence never depends on delivery success.</summary>
public sealed class TelegramTaskNotificationService : ITelegramTaskNotificationService
{
    private readonly HttpClient _client;
    private readonly TelegramBotOptions _options;
    private readonly ILogger<TelegramTaskNotificationService> _logger;
    public TelegramTaskNotificationService(HttpClient client, IOptions<TelegramBotOptions> options, ILogger<TelegramTaskNotificationService> logger) { _client = client; _options = options.Value; _logger = logger; }

    public async Task<string> SendTaskAssignedAsync(long chatId, string projectName, string taskTitle, string description, DateTime? dueAt, CancellationToken ct = default)
    {
        var text = $"📌 Yeni tapşırıq\nLayihə: {projectName}\nTapşırıq: {taskTitle}";
        if (!string.IsNullOrWhiteSpace(description)) text += $"\nQeyd: {description}";
        if (dueAt.HasValue) text += $"\nSon tarix: {dueAt.Value.ToLocalTime():dd.MM.yyyy}";
        return await SendAsync(chatId, text, ct);
    }

    public Task<string> SendProjectApprovedAsync(long chatId, string projectName, CancellationToken ct = default)
        => SendAsync(chatId, $"✅ Layihə qəbul edildi\nLayihə: {projectName}\nLayihə artıq İcra olunan layihələr bölməsinə əlavə edildi.", ct);

    public Task<string> SendProjectApprovalRequestAsync(long chatId, int requestId, string environmentScope, StakeholderApprovalNotification project, bool useRussian, CancellationToken ct = default)
        => SendAsync(chatId, FormatApprovalRequest(project, useRussian), ct,
            new { inline_keyboard = new[] { new[] { new { text = useRussian ? "✅ Одобрить" : "✅ Təsdiqlə", callback_data = $"project-approval:{environmentScope}:{requestId}:approve" }, new { text = useRussian ? "⛔ Отклонить" : "⛔ Rədd et", callback_data = $"project-approval:{environmentScope}:{requestId}:decline" } } } }, "HTML");

    public Task<string> SendWhatsappInteractionAsync(
        long chatId,
        WhatsappInteractionNotification notification,
        CancellationToken ct = default)
        => SendAsync(chatId, FormatWhatsappInteraction(notification), ct, parseMode: "HTML");

    private async Task<string> SendAsync(long chatId, string text, CancellationToken ct, object? replyMarkup = null, string? parseMode = null)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken)) return "PendingBotConfiguration";
        try
        {
            // The bot token contains a colon. It must be part of a fully-qualified URI;
            // otherwise .NET interprets `bot<id>` as a custom URI scheme.
            var endpoint = new Uri($"https://api.telegram.org/bot{_options.BotToken}/sendMessage", UriKind.Absolute);
            var payload = new Dictionary<string, object?>
            {
                ["chat_id"] = chatId,
                ["text"] = text
            };
            if (replyMarkup is not null) payload["reply_markup"] = replyMarkup;
            if (!string.IsNullOrWhiteSpace(parseMode)) payload["parse_mode"] = parseMode;
            if (text.Contains("http://", StringComparison.OrdinalIgnoreCase) || text.Contains("https://", StringComparison.OrdinalIgnoreCase))
                payload["link_preview_options"] = new { is_disabled = true };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(payload)
            };
            using var response = await _client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return "Delivered";
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Telegram notification was rejected with HTTP {StatusCode}. Response={Response}", (int)response.StatusCode, TrimForLog(errorBody));
            return $"DeliveryFailedHttp{(int)response.StatusCode}";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return "DeliveryTimedOut"; }
        catch (Exception exception)
        {
            // Do not pass the exception object to the logger: HttpClient exceptions can
            // include the absolute Telegram URL, which contains the bot token.
            _logger.LogWarning(
                "Telegram notification could not be delivered. ExceptionType={ExceptionType} Message={Message}",
                exception.GetType().Name, RedactToken(exception.Message));
            return "DeliveryFailedTransport";
        }
    }

    private string RedactToken(string? message)
        => string.IsNullOrWhiteSpace(message) || string.IsNullOrWhiteSpace(_options.BotToken)
            ? message ?? string.Empty
            : message.Replace(_options.BotToken, "[redacted]", StringComparison.Ordinal);

    private static string TrimForLog(string? value)
    {
        var trimmed = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    internal static string FormatApprovalRequest(StakeholderApprovalNotification project, bool useRussian)
    {
        var language = useRussian ? Russian : Azerbaijani;
        var text = new StringBuilder();
        text.AppendLine($"<b>{language.Title}</b>");
        AppendField(text, language.SentBy, project.SentBy);
        text.AppendLine($"<b>{language.Project}:</b> {Encode(project.ProjectName)}");
        AppendField(text, language.Location, project.Location);
        AppendField(text, language.Contact, JoinContact(project.ContactName, project.PhoneNumber));
        text.AppendLine($"<b>{language.Value}:</b> {project.OfferPrice:N2} AZN");
        AppendField(text, language.ProjectDate, FormatDate(project.ProjectDate));
        AppendField(text, language.ResponseExpected, FormatDate(project.ResponseExpectedAt));

        if (project.Offers.Count > 0)
        {
            text.AppendLine();
            text.AppendLine($"<b>{language.Offers}</b>");
            foreach (var offer in project.Offers.Take(5))
            {
                var details = string.Join(" · ", new[]
                {
                    offer.Power > 0 ? $"{offer.Power:N2} kW" : string.Empty,
                    offer.AreaType,
                    MountLabel(offer.MountType, useRussian),
                    offer.ExtraAmount > 0 ? $"{offer.ExtraAmount:N2} AZN" : string.Empty
                }.Where(x => !string.IsNullOrWhiteSpace(x)));
                text.AppendLine($"• {Encode(details)}");
            }
        }

        AppendField(text, language.Note, Trim(project.SmallNote, 300));
        AppendField(text, language.Description, Trim(project.Description, 900));

        var attachments = project.Attachments.Take(5).Where(x => IsSafeHttpUrl(x.FilePath)).ToList();
        if (attachments.Count > 0)
        {
            text.AppendLine();
            text.AppendLine($"<b>{language.Attachments}</b>");
            foreach (var attachment in attachments)
            {
                var label = string.IsNullOrWhiteSpace(attachment.Label) ? attachment.FileName : attachment.Label;
                text.AppendLine($"• <a href=\"{WebUtility.HtmlEncode(attachment.FilePath)}\">{Encode(Trim(label, 120))}</a>");
            }
        }

        text.AppendLine();
        text.Append(language.Action);
        return Trim(text.ToString(), 4000);
    }

    internal static string FormatWhatsappInteraction(WhatsappInteractionNotification notification)
    {
        try
        {
            using var document = JsonDocument.Parse(notification.PayloadJson);
            var root = document.RootElement;
            var text = new StringBuilder();
            var isYoxla = notification.Topic == WhatsappNotificationTopics.Yoxla;
            text.AppendLine(isYoxla
                ? "📦 <b>YENİ STOK SORĞUSU</b>"
                : "☀️ <b>YENİ GÜNƏŞ QİYMƏTLƏNDİRMƏSİ</b>");
            text.AppendLine(isYoxla
                ? "<i>Müştəri stokda olmayan məhsul üçün “Yoxla” düyməsini seçdi.</i>"
                : "<i>Müştəri hesablamadan sonra WhatsApp əlaqəsini seçdi.</i>");
            text.AppendLine();
            text.AppendLine($"🕒 <b>Vaxt:</b> {notification.OccurredAt:dd.MM.yyyy HH:mm}");

            var pagePath = ReadNestedText(root, "page", "path");
            var placement = ReadText(root, "placement");
            var contactPhone = ReadText(root, "contactPhone");
            if (!string.IsNullOrWhiteSpace(pagePath)) text.AppendLine($"📍 <b>Səhifə:</b> <code>{Encode(pagePath)}</code>");
            if (!string.IsNullOrWhiteSpace(placement)) text.AppendLine($"🔎 <b>Mənbə:</b> <code>{Encode(placement)}</code>");
            if (!string.IsNullOrWhiteSpace(notification.Language)) text.AppendLine($"🌐 <b>Dil:</b> {Encode(notification.Language.ToUpperInvariant())}");
            if (!string.IsNullOrWhiteSpace(contactPhone)) text.AppendLine($"📞 <b>Telefon:</b> {Encode(Trim(contactPhone, 40))}");

            if (isYoxla)
            {
                var productItems = new List<JsonElement>();
                if (root.TryGetProperty("product", out var product) && product.ValueKind == JsonValueKind.Object)
                    productItems.Add(product);
                if (root.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in products.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).Take(10))
                        productItems.Add(item);
                }

                if (productItems.Count > 0)
                {
                    text.AppendLine();
                    text.AppendLine(productItems.Count == 1 ? "🛒 <b>MƏHSUL</b>" : $"🛒 <b>MƏHSULLAR ({productItems.Count})</b>");
                    for (var index = 0; index < productItems.Count; index++)
                        AppendProductCard(text, productItems[index], index + 1, productItems.Count > 1);
                }
            }
            else
            {
                var quoteDetails = ReadNestedText(root, "context", "quoteDetails");
                if (!string.IsNullOrWhiteSpace(quoteDetails))
                {
                    text.AppendLine();
                    text.AppendLine("📊 <b>HESABLAMA</b>");
                    text.AppendLine($"<blockquote>{Encode(Trim(quoteDetails, 1_500))}</blockquote>");
                }
                else
                {
                    AppendCalculatorSummary(text, root);
                }
            }

            var prefilledMessage = ReadText(root, "prefilledMessage");
            if (!string.IsNullOrWhiteSpace(prefilledMessage))
            {
                text.AppendLine();
                text.AppendLine("💬 <b>WHATSAPP MESAJI</b>");
                text.AppendLine($"<blockquote>{Encode(Trim(prefilledMessage, 1_200))}</blockquote>");
            }

            var deviceId = ReadNestedText(root, "requestMetadata", "deviceId");
            var interactionId = ReadNestedText(root, "requestMetadata", "interactionId");
            if (!string.IsNullOrWhiteSpace(deviceId) || !string.IsNullOrWhiteSpace(interactionId))
            {
                text.AppendLine();
                text.AppendLine("<blockquote expandable><b>🔐 Texniki məlumat</b>");
                if (!string.IsNullOrWhiteSpace(deviceId)) text.AppendLine($"Device: <code>{Encode(Trim(deviceId, 100))}</code>");
                if (!string.IsNullOrWhiteSpace(interactionId)) text.AppendLine($"Interaction: <code>{Encode(Trim(interactionId, 100))}</code>");
                text.AppendLine("</blockquote>");
            }

            return Trim(text.ToString(), 3_900);
        }
        catch (JsonException)
        {
            return notification.Topic == WhatsappNotificationTopics.Yoxla
                ? "📦 Yeni stok sorğusu — Yoxla\nDetalları Volt Analytics bölməsində yoxlayın."
                : "☀️ Yeni günəş qiymətləndirmə sorğusu\nDetalları Volt Analytics bölməsində yoxlayın.";
        }
    }

    private static void AppendProductCard(StringBuilder text, JsonElement product, int index, bool showIndex)
    {
        var name = ReadText(product, "name");
        if (string.IsNullOrWhiteSpace(name)) return;
        var brand = ReadText(product, "brand");
        var variant = ReadText(product, "variant");
        var requested = ReadNumber(product, "requestedQuantity");
        var available = ReadNumber(product, "availableStock");
        text.AppendLine();
        text.AppendLine(showIndex
            ? $"<b>{index}. {Encode(Trim(name, 300))}</b>"
            : $"<b>{Encode(Trim(name, 300))}</b>");
        if (!string.IsNullOrWhiteSpace(brand)) text.AppendLine($"🏷 <b>Brend:</b> {Encode(brand)}");
        if (!string.IsNullOrWhiteSpace(variant)) text.AppendLine($"⚙️ <b>Variant:</b> {Encode(variant)}");
        text.Append($"🔢 <b>İstək:</b> {(requested ?? 1):0.##}");
        if (available.HasValue) text.Append($"  ·  <b>Stok:</b> {available.Value:0.##}");
        text.AppendLine();
    }

    private static void AppendCalculatorSummary(StringBuilder text, JsonElement root)
    {
        if (!root.TryGetProperty("context", out var context) || context.ValueKind != JsonValueKind.Object) return;
        var inputs = context.TryGetProperty("inputs", out var inputValue) ? inputValue : default;
        var result = context.TryGetProperty("result", out var resultValue) ? resultValue : default;
        var summary = new List<string>();
        AddSummary(summary, "Şəhər", ReadText(inputs, "city"));
        AddSummary(summary, "Obyekt", ReadText(inputs, "propertyType"));
        AddSummary(summary, "Sistem", ReadText(inputs, "systemType"));
        AddSummary(summary, "Aylıq ödəniş", FormatNumber(ReadNumber(inputs, "bill"), " AZN"));
        AddSummary(summary, "Güc", FormatNumber(ReadNumber(result, "power"), " kW"));
        AddSummary(summary, "Qiymət", FormatNumber(ReadNumber(result, "price"), " AZN"));
        if (summary.Count == 0) return;
        text.AppendLine();
        text.AppendLine("📊 <b>HESABLAMA</b>");
        foreach (var line in summary) text.AppendLine($"• {Encode(line)}");
    }

    private static void AddSummary(ICollection<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) lines.Add($"{label}: {value}");
    }

    private static string FormatNumber(decimal? value, string suffix)
        => value.HasValue ? $"{value.Value:0.##}{suffix}" : string.Empty;

    private static string ReadNestedText(JsonElement root, string objectName, string propertyName)
        => root.ValueKind == JsonValueKind.Object
           && root.TryGetProperty(objectName, out var nested)
           && nested.ValueKind == JsonValueKind.Object
            ? ReadText(nested, propertyName)
            : string.Empty;

    private static string ReadText(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value)) return string.Empty;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => string.Empty
        };
    }

    private static decimal? ReadNumber(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out number) ? number : null;
    }

    private static void AppendField(StringBuilder text, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) text.AppendLine($"<b>{label}:</b> {Encode(value)}");
    }

    private static string JoinContact(string name, string phone)
        => string.Join(" · ", new[] { name, phone }.Where(x => !string.IsNullOrWhiteSpace(x)));
    private static string FormatDate(DateTime? value) => value?.ToString("dd.MM.yyyy") ?? string.Empty;
    private static string MountLabel(string value, bool russian) => value == "ground" ? (russian ? "наземный" : "yerüstü") : (russian ? "кровля" : "dam");
    private static string Encode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().Length <= max ? value.Trim() : value.Trim()[..Math.Max(0, max - 1)] + "…";
    private static bool IsSafeHttpUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private sealed record ApprovalLanguage(string Title, string SentBy, string Project, string Location, string Contact, string Value, string ProjectDate, string ResponseExpected, string Offers, string Note, string Description, string Attachments, string Action);
    private static readonly ApprovalLanguage Azerbaijani = new("🟡 Stakeholder təsdiqi tələb olunur", "Göndərən", "Layihə", "Lokasiya", "Əlaqəli şəxs", "Təklif məbləği (ƏDV daxil)", "Layihə tarixi", "Gözlənilən cavab", "Təkliflər", "Qısa qeyd", "Ətraflı məlumat", "Əlavələr", "Zəhmət olmasa qərarınızı aşağıdakı düymələrdən seçin.");
    private static readonly ApprovalLanguage Russian = new("🟡 Требуется согласование stakeholder", "Отправил", "Проект", "Локация", "Контактное лицо", "Стоимость предложения (с НДС)", "Дата проекта", "Ожидаемый ответ", "Предложения", "Краткая заметка", "Подробности", "Вложения", "Пожалуйста, выберите решение кнопкой ниже.");
}
