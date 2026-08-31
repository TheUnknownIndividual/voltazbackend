namespace Volt.Application.Dtos.SolarAnalytics;

public static class WhatsappNotificationTopics
{
    public const string Yoxla = "yoxla";
    public const string Qiymetlendirme = "qiymetlendirme";
}

public sealed record WhatsappInteractionNotification(
    string Topic,
    string Language,
    string PayloadJson,
    DateTime OccurredAt);
