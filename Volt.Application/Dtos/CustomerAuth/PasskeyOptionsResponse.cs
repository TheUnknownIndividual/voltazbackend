namespace Volt.Application.Dtos.CustomerAuth
{
    public sealed record PasskeyOptionsResponse(string ChallengeId, string PublicKeyOptionsJson);
}
