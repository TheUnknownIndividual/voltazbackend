using System.ComponentModel.DataAnnotations;

namespace Volt.Application.Dtos.Project
{
    public class ProjectUpdateRequest
    {
        [Required]
        [MinLength(1)]
        public List<ProjectLanguageUpdateRequest> Languages { get; set; }

        public int TotalPower { get; set; }
        public byte PowerType { get; set; }
        public int AnnualProduction { get; set; }
        public byte AnnualProductionType { get; set; }
        public byte SystemType { get; set; }
        [MaxLength(120)]
        public string? ContactFullName { get; set; }
        [MaxLength(40)]
        public string? ContactPhone { get; set; }
        public DateTime? ProjectDate { get; set; }
        public DateTime? InquiryReceivedAt { get; set; }
        public DateTime? OfferSentAt { get; set; }
        public DateTime? ResponseExpectedAt { get; set; }
        [MaxLength(80)]
        public string? CurrentStatus { get; set; }
        [MaxLength(140)]
        public string? ShortNote { get; set; }
        public decimal? OfferAmountAzn { get; set; }
        public List<string>? ImagePaths { get; set; }
        public List<ProjectAttachmentRequest>? Attachments { get; set; }
        public List<ProjectOfferRequest>? Offers { get; set; }
        public List<string>? NewImagePaths { get; set; }
        public List<int>? DeleteImageIds { get; set; }
    }
}
