namespace Volt.Domain.Entities;

public sealed class HomeSliderSettings
{
    public int Id { get; set; }
    public string SlidesJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; }
}
