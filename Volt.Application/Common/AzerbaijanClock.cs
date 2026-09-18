namespace Volt.Application.Common
{
    // Azerbaijan has used a fixed UTC+4 offset with no DST since 2016, so a single
    // TimeZoneInfo lookup (with a Windows-id fallback for hosts without IANA data) is
    // sufficient -- this mirrors the same try/catch idiom previously duplicated across
    // SolarAnalyticsService/DocumentVerificationService/DocumentVerificationInquiryService.
    public static class AzerbaijanClock
    {
        private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

        public static DateTime ToUtc(DateTime localUnspecified)
        {
            var local = DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(local, TimeZone);
        }

        private static TimeZoneInfo ResolveTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time"); }
        }
    }
}
