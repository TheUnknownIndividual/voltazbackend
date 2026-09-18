using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class ScrapedNewsItemConfiguration : IEntityTypeConfiguration<ScrapedNewsItem>
    {
        public void Configure(EntityTypeBuilder<ScrapedNewsItem> builder)
        {
            builder.ToTable("ScrapedNewsItems");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SourceSite).IsRequired().HasMaxLength(32);
            builder.Property(x => x.SourceUrl).IsRequired().HasMaxLength(450);
            builder.Property(x => x.SourceTitle).IsRequired().HasMaxLength(500);
            builder.Property(x => x.SourceListingImageUrl).HasMaxLength(1000);
            builder.Property(x => x.RawBodyText);
            builder.Property(x => x.RehostedImageUrl).HasMaxLength(1000);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
            builder.Property(x => x.RelevanceReason).HasMaxLength(1000);
            builder.Property(x => x.PublishedContentType).HasMaxLength(16);
            builder.Property(x => x.ErrorCode).HasMaxLength(100);
            builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
            builder.HasIndex(x => x.SourceUrl).IsUnique();
            builder.HasIndex(x => new { x.SourceSite, x.SourcePublishedAt });
            builder.HasIndex(x => x.Status);
        }
    }
}
