using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class ProductParametrConfiguration : IEntityTypeConfiguration<ProductParametr>
    {
        public void Configure(EntityTypeBuilder<ProductParametr> builder)
        {
            builder.ToTable("ProductParametrs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ModelLabel)
                .HasMaxLength(200)
                .IsRequired(false);

            builder.Property(x => x.Effectiveness)
                .HasColumnType("decimal(18,2)")
                .IsRequired(false);

            builder.Property(x => x.Count)
                .IsRequired();

            builder.Property(x => x.Amount)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.ProductId)
                .IsRequired();

            builder.HasIndex(x => x.ProductId);
        }
    }
}
