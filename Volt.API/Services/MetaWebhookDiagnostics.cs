#nullable enable

using Volt.Application.Dtos.MetaInbox;

namespace Volt.API.Services
{
    /// <summary>
    /// Keeps a non-sensitive, process-local summary of webhook health for the admin inbox.
    /// No payloads, participant identifiers, access tokens, or secrets are retained.
    /// </summary>
    public sealed class MetaWebhookDiagnostics
    {
        private readonly object _gate = new();
        private readonly DateTime _startedAtUtc = DateTime.UtcNow;
        private long _attemptCount;
        private long _acceptedCount;
        private long _rejectedCount;
        private long _failedCount;
        private long _lastCompletedAttemptId;
        private DateTime? _lastAttemptAtUtc;
        private DateTime? _lastCompletedAtUtc;
        private DateTime? _lastAcceptedAtUtc;
        private int? _lastResponseStatus;
        private string _lastResult = "waiting";
        private MetaInboxWebhookProcessingResult? _lastProcessingResult;

        public long BeginAttempt()
        {
            lock (_gate)
            {
                _attemptCount++;
                _lastAttemptAtUtc = DateTime.UtcNow;
                return _attemptCount;
            }
        }

        public void CompleteAccepted(long attemptId, MetaInboxWebhookProcessingResult result)
        {
            lock (_gate)
            {
                _acceptedCount++;
                _lastAcceptedAtUtc = DateTime.UtcNow;
                CompleteLatest(attemptId, 200, "accepted", result);
            }
        }

        public void CompleteRejected(long attemptId, int statusCode, string result)
        {
            lock (_gate)
            {
                _rejectedCount++;
                CompleteLatest(attemptId, statusCode, result, null);
            }
        }

        public void CompleteFailed(long attemptId, int statusCode, string result)
        {
            lock (_gate)
            {
                _failedCount++;
                CompleteLatest(attemptId, statusCode, result, null);
            }
        }

        public MetaInboxWebhookDiagnosticsDto Snapshot()
        {
            lock (_gate)
            {
                return new MetaInboxWebhookDiagnosticsDto(
                    _startedAtUtc,
                    _lastAttemptAtUtc,
                    _lastCompletedAtUtc,
                    _lastAcceptedAtUtc,
                    _lastResponseStatus,
                    _lastResult,
                    _attemptCount,
                    _acceptedCount,
                    _rejectedCount,
                    _failedCount,
                    _lastProcessingResult?.ObjectType,
                    _lastProcessingResult?.Field,
                    _lastProcessingResult?.PhoneNumberId,
                    _lastProcessingResult?.MessageCount ?? 0,
                    _lastProcessingResult?.StatusCount ?? 0);
            }
        }

        private void CompleteLatest(
            long attemptId,
            int statusCode,
            string result,
            MetaInboxWebhookProcessingResult? processingResult)
        {
            if (attemptId < _lastCompletedAttemptId) return;

            _lastCompletedAttemptId = attemptId;
            _lastCompletedAtUtc = DateTime.UtcNow;
            _lastResponseStatus = statusCode;
            _lastResult = result;
            _lastProcessingResult = processingResult;
        }
    }
}
