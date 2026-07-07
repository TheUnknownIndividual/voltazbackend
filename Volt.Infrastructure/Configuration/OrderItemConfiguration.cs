using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("OrderItems");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ProductName).IsRequired().HasMaxLength(240);
            builder.Property(x => x.ProductImageUrl).HasMaxLength(500);
            builder.Property(x => x.SelectedPower).HasMaxLength(120);
            builder.Property(x => x.Quantity).IsRequired();
            builder.Property(x => x.ReservedQuantity).IsRequired().HasDefaultValue(0);
            builder.Property(x => x.InventoryReleased).IsRequired().HasDefaultValue(false);
            builder.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
            builder.Property(x => x.LineTotal).HasColumnType("decimal(18,2)").IsRequired();

            builder.HasIndex(x => x.OrderId);
            builder.HasIndex(x => x.ProductId);
            builder.HasIndex(x => x.ProductParametrId);

            builder.HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
