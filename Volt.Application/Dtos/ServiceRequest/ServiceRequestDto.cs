using Volt.Domain.Enums;

namespace Volt.Application.Dtos.ServiceRequest
{
    public sealed record ServiceRequestDto(
        int Id,
        string Name,
        string Surname,
        string Email,
        string Phone,
        string Message,
        byte Status,
        int ServiceManagementId,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        bool IsActive
    );
}
