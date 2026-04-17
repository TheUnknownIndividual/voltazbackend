using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class BlogTranslationConfiguration : IEntityTypeConfiguration<BlogTranslation>
    {
        public void Configure(EntityTypeBuilder<BlogTranslation> builder)
        {
            builder.ToTable("BlogTranslations");

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

            builder.HasIndex(x => new { x.BlogId, x.LanguageCode })
                .IsUnique();

            builder.HasOne(x => x.Blog)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.BlogId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
