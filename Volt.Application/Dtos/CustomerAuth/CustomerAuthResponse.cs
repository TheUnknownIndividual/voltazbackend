namespace Volt.Application.Dtos.CustomerAuth
{
    public sealed record CustomerAuthResponse(string AccessToken, CustomerDto User);
}
