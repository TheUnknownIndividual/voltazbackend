namespace Volt.Domain.Entities
{
    public sealed class DocumentSequence
    {
        public int Id { get; set; }
        public string DocumentCode { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int CurrentNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
