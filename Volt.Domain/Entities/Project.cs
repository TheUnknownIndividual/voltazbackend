namespace Volt.Domain.Entities
{
    public class Project
    {
        public int Id { get; set; }
        public int TotalPower { get; set; }
        public byte PowerType { get; set; }
        public int AnnualProduction { get; set; }
        public byte AnnualProductionType { get; set; }
        public byte SystemType { get; set; }
        public string? ContactFullName { get; set; }
        public string? ContactPhone { get; set; }
        public DateTime? ProjectDate { get; set; }
        public DateTime? InquiryReceivedAt { get; set; }
        public DateTime? OfferSentAt { get; set; }
        public DateTime? ResponseExpectedAt { get; set; }
        public string? CurrentStatus { get; set; }
        public string? ShortNote { get; set; }
        public decimal? OfferAmountAzn { get; set; }
        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public ICollection<ProjectLanguage> Languages { get; set; } = new List<ProjectLanguage>();
        public ICollection<ProjectImage> Images { get; set; } = new List<ProjectImage>();
        public ICollection<ProjectAttachment> Attachments { get; set; } = new List<ProjectAttachment>();
        public ICollection<ProjectOffer> Offers { get; set; } = new List<ProjectOffer>();
    }
}
