#nullable enable

using Volt.Application.Dtos.MetaInbox;
using Volt.Application.Dtos;

namespace Volt.Application.Interfaces
{
    public interface IMetaInboxHistorySyncService
    {
        Task<MetaInboxHistorySyncDto?> GetStatusAsync(
            string phoneNumberId,
            CancellationToken ct = default);

        Task<ApiResponse<MetaInboxHistorySyncDto>> RequestAsync(
            string phoneNumberId,
            int actorAdminUserId,
            CancellationToken ct = default);

        Task<MetaInboxHistorySyncDto> RecordProgressAsync(
            MetaInboxHistorySyncProgressUpdate update,
            CancellationToken ct = default);

        Task<MetaInboxHistorySyncDto> RecordFailureAsync(
            MetaInboxHistorySyncFailureUpdate update,
            CancellationToken ct = default);
    }
}
