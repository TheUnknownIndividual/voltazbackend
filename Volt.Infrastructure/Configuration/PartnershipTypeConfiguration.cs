using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class PartnershipTypeConfiguration : IEntityTypeConfiguration<PartnershipType>
    {
        public void Configure(EntityTypeBuilder<PartnershipType> builder)
        {
            builder.ToTable("PartnershipTypes");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true);
        }
    }
}
