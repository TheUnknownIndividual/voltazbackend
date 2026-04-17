using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class NewsPostLanguageConfiguration : IEntityTypeConfiguration<NewsPostLanguage>
    {
        public void Configure(EntityTypeBuilder<NewsPostLanguage> builder)
        {
            builder.ToTable("NewsPostLanguages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.LanguageCode)
                .IsRequired();

            builder.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(x => x.Content)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(x => new { x.NewsPostId, x.LanguageCode })
                .IsUnique();

            builder.HasOne(x => x.NewsPost)
                .WithMany(x => x.Languages)
                .HasForeignKey(x => x.NewsPostId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
