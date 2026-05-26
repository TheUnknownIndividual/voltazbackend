using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class ProductDescriptionLanguageConfiguration : IEntityTypeConfiguration<ProductDescriptionLanguage>
    {
        public void Configure(EntityTypeBuilder<ProductDescriptionLanguage> builder)
        {
            builder.ToTable("ProductDescriptionLanguages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ProductDescriptionId)
                .IsRequired();

            builder.Property(x => x.LanguageCode)
                .IsRequired();

            builder.Property(x => x.Description)
                .IsRequired();

            builder.Property(x => x.Features)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(x => new { x.ProductDescriptionId, x.LanguageCode })
                .IsUnique();

            builder.HasOne(x => x.ProductDescription)
                .WithMany(x => x.Languages)
                .HasForeignKey(x => x.ProductDescriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
