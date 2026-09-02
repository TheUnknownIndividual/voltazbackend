namespace Volt.Application.Dtos.GoogleReviews
{
    public sealed class GoogleReviewDto
    {
        public string ReviewerName { get; set; } = string.Empty;
        public string ReviewerPhotoUrl { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Text { get; set; } = string.Empty;
        public string RelativeTime { get; set; } = string.Empty;
        public long PublishTime { get; set; }
    }

    public sealed class GoogleReviewsResultDto
    {
        public double? OverallRating { get; set; }
        public int? UserRatingCount { get; set; }
        public string GoogleMapsUrl { get; set; } = string.Empty;
        public IReadOnlyList<GoogleReviewDto> Reviews { get; set; } = Array.Empty<GoogleReviewDto>();
    }
}
