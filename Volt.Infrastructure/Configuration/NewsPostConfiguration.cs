using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class NewsPostConfiguration : IEntityTypeConfiguration<NewsPost>
    {
        public void Configure(EntityTypeBuilder<NewsPost> builder)
        {
            builder.ToTable("NewsPosts");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.CoverImagePath)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.Source)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.PostLink)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .IsRequired(false);

            builder.HasMany(a => a.Languages)
                .WithOne(ad => ad.NewsPost)
                .HasForeignKey(ad => ad.NewsPostId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
