using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class AdminTrackedProjectOfferConfiguration : IEntityTypeConfiguration<AdminTrackedProjectOffer>
    {
        public void Configure(EntityTypeBuilder<AdminTrackedProjectOffer> builder)
        {
            builder.ToTable("AdminTrackedProjectOffers");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Power).HasColumnType("decimal(18,2)");
            builder.Property(x => x.MountType).IsRequired().HasMaxLength(20);
            builder.Property(x => x.AreaType).HasMaxLength(120);
            builder.Property(x => x.ExtraAmount).HasColumnType("decimal(18,2)");
            builder.Property(x => x.SentAt);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
        }
    }
}
