namespace Volt.Domain.Enums
{
    public enum OrderPaymentStatus : byte
    {
        Pending = 1,
        AwaitingProvider = 2,
        Paid = 3,
        Failed = 4,
        Refunded = 5,
        NotRequiredYet = 6
    }
}
