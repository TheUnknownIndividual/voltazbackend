using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class ProductParametrLanguageConfiguration : IEntityTypeConfiguration<ProductParametrLanguage>
    {
        public void Configure(EntityTypeBuilder<ProductParametrLanguage> builder)
        {
            builder.ToTable("ProductParametrLanguages");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Description).IsRequired().HasDefaultValue(string.Empty);
            builder.Property(x => x.Features).IsRequired().HasDefaultValue(string.Empty);
            builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
            builder.HasIndex(x => new { x.ProductParametrId, x.LanguageCode }).IsUnique();
            builder.HasOne(x => x.ProductParametr)
                .WithMany(x => x.Languages)
                .HasForeignKey(x => x.ProductParametrId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
