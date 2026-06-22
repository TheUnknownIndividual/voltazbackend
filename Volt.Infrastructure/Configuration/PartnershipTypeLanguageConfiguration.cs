using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class PartnershipTypeLanguageConfiguration : IEntityTypeConfiguration<PartnershipTypeLanguage>
    {
        public void Configure(EntityTypeBuilder<PartnershipTypeLanguage> builder)
        {
            builder.ToTable("PartnershipTypeLanguages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired();

            builder.Property(x => x.LanguageCode)
                .IsRequired();

            builder.HasOne(x => x.PartnershipType)
                .WithMany(x => x.Languages)
                .HasForeignKey(x => x.PartnershipTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.PartnershipTypeId, x.LanguageCode })
                .IsUnique();

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true);
        }
    }
}
