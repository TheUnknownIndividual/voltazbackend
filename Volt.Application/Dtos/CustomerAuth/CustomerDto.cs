namespace Volt.Application.Dtos.CustomerAuth
{
    public sealed record CustomerDto(
        int Id,
        string FirstName,
        string LastName,
        string Name,
        string Email,
        string Phone,
        string Address,
        string Role,
        DateTime CreatedAt);
}
