#nullable enable

using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Volt.API.Services
{
    public sealed record MetaWhatsAppOnboardingSession(
        string AccessToken,
        string WhatsAppBusinessAccountId,
        string PhoneNumberId,
        DateTime ExpiresAtUtc);

    /// <summary>
    /// Keeps Meta's temporary Embedded Signup token in process memory only while a
    /// super admin completes phone registration. Expired sessions are removed on access.
    /// </summary>
    public sealed class MetaWhatsAppOnboardingSessionStore
    {
        private readonly ConcurrentDictionary<string, MetaWhatsAppOnboardingSession> _sessions = new(StringComparer.Ordinal);

        public string Create(string accessToken, string wabaId, string phoneNumberId)
        {
            PruneExpired();
            var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            _sessions[key] = new MetaWhatsAppOnboardingSession(accessToken, wabaId, phoneNumberId, DateTime.UtcNow.AddMinutes(10));
            return key;
        }

        public bool TryGet(string key, out MetaWhatsAppOnboardingSession? session)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(key) || !_sessions.TryGetValue(key, out var candidate)) return false;
            if (candidate.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _sessions.TryRemove(key, out _);
                return false;
            }
            session = candidate;
            return true;
        }

        public void Remove(string key) => _sessions.TryRemove(key, out _);

        private void PruneExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var item in _sessions)
                if (item.Value.ExpiresAtUtc <= now) _sessions.TryRemove(item.Key, out _);
        }
    }
}
