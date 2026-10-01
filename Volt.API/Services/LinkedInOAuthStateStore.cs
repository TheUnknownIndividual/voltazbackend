using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Volt.API.Services
{
    // CSRF protection for the LinkedIn "Connect" OAuth flow: a random state value is issued when
    // the admin starts the flow and must come back unchanged on LinkedIn's redirect. In-memory only,
    // short-lived, single use -- same shape as MetaWhatsAppOnboardingSessionStore.
    public sealed class LinkedInOAuthStateStore
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
        private readonly ConcurrentDictionary<string, DateTime> _states = new();

        public string Create()
        {
            Prune();
            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            _states[state] = DateTime.UtcNow.Add(Ttl);
            return state;
        }

        public bool TryConsume(string state)
        {
            if (string.IsNullOrWhiteSpace(state)) return false;
            if (!_states.TryRemove(state, out var expiresAt)) return false;
            return expiresAt >= DateTime.UtcNow;
        }

        private void Prune()
        {
            var now = DateTime.UtcNow;
            foreach (var pair in _states)
                if (pair.Value < now) _states.TryRemove(pair.Key, out _);
        }
    }
}
