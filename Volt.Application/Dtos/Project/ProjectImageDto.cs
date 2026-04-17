namespace Volt.Application.Dtos.Project
{
    public sealed record ProjectImageDto(
        int Id,
        string ImagePath,
        bool IsActive
    );
}
