using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class ProjectOfferConfiguration : IEntityTypeConfiguration<ProjectOffer>
    {
        public void Configure(EntityTypeBuilder<ProjectOffer> builder)
        {
            builder.ToTable("ProjectOffers");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Power)
                .HasColumnType("decimal(18,2)");

            builder.Property(x => x.AreaType)
                .IsRequired()
                .HasMaxLength(120);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);
        }
    }
}
