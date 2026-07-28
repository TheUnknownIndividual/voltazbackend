namespace Volt.Domain.Entities
{
    public sealed class AdminTrackedProject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string PersonName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime? ProjectDate { get; set; }
        public DateTime? InquiryReceivedAt { get; set; }
        public DateTime? OfferSentAt { get; set; }
        public DateTime? ResponseExpectedAt { get; set; }
        public byte? SystemType { get; set; }
        public string CurrentStatus { get; set; } = string.Empty;
        public string SmallNote { get; set; } = string.Empty;
        public decimal OfferPrice { get; set; }
        public bool IsOfferPriceManual { get; set; }
        public bool IncludesAdv { get; set; }
        public string Description { get; set; } = string.Empty;
        public string StakeholderApprovalStatus { get; set; } = "NotRequired";
        public DateTime? StakeholderApprovalRequestedAt { get; set; }
        public DateTime? StakeholderApprovalResolvedAt { get; set; }
        public int? StakeholderApprovalResolvedByAdminUserId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public ICollection<AdminTrackedProjectOffer> Offers { get; set; } = new List<AdminTrackedProjectOffer>();
        public ICollection<AdminTrackedProjectAttachment> Attachments { get; set; } = new List<AdminTrackedProjectAttachment>();
    }
}
