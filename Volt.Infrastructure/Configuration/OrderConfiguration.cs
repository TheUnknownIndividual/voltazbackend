using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.OrderNumber).IsRequired().HasMaxLength(40);
            builder.HasIndex(x => x.OrderNumber).IsUnique();

            builder.Property(x => x.Source).IsRequired().HasDefaultValue((byte)1);
            builder.Property(x => x.Intent).IsRequired().HasDefaultValue((byte)1);
            builder.Property(x => x.IsViewedByAdmin).IsRequired().HasDefaultValue(false);
            builder.Property(x => x.AcceptedTerms).IsRequired().HasDefaultValue(false);

            builder.Property(x => x.FullName).IsRequired().HasMaxLength(160);
            builder.Property(x => x.Phone).IsRequired().HasMaxLength(40);
            builder.Property(x => x.Email).IsRequired().HasMaxLength(180);
            builder.Property(x => x.CityOrRegion).HasMaxLength(120);
            builder.Property(x => x.District).HasMaxLength(120);
            builder.Property(x => x.StreetAndBuilding).HasMaxLength(240);
            builder.Property(x => x.ApartmentOrOffice).HasMaxLength(120);
            builder.Property(x => x.DeliveryNotes).HasMaxLength(800);
            builder.Property(x => x.PickupLocation).HasMaxLength(160);

            builder.Property(x => x.ProductsSubtotal).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(x => x.DeliveryFee).HasColumnType("decimal(18,2)").IsRequired(false);
            builder.Property(x => x.DiscountTotal).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(x => x.FinalTotal).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
            builder.Property(x => x.CreatedAt).IsRequired();

            builder.HasMany(x => x.Items)
                .WithOne(x => x.Order)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
