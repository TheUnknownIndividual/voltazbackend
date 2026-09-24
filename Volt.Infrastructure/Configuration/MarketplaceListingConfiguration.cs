using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class MarketplaceListingConfiguration : IEntityTypeConfiguration<MarketplaceListing>
    {
        public void Configure(EntityTypeBuilder<MarketplaceListing> builder)
        {
            builder.ToTable("MarketplaceListings");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Marketplace).IsRequired().HasMaxLength(16);
            builder.Property(x => x.ExternalId).IsRequired().HasMaxLength(64);
            builder.Property(x => x.Url).HasMaxLength(500);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
            builder.HasIndex(x => x.ProductId);
            builder.HasIndex(x => new { x.Marketplace, x.ExternalId }).IsUnique();
        }
    }
}
