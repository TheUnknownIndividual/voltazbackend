using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class HomeSliderSettingsConfiguration : IEntityTypeConfiguration<HomeSliderSettings>
{
    public void Configure(EntityTypeBuilder<HomeSliderSettings> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SlidesJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
