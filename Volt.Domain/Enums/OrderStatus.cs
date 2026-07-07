namespace Volt.Domain.Enums
{
    public enum OrderStatus : byte
    {
        New = 1,
        Confirming = 2,
        AwaitingPayment = 3,
        Processing = 4,
        Completed = 5,
        Cancelled = 6
    }
}
