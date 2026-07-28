namespace Volt.Application.Dtos.Project
{
    public sealed record ProjectAttachmentDto(
        int Id,
        string FilePath,
        string? Label,
        bool IsActive
    );
}
