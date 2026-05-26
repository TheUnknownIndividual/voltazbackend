using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.ProductName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasOne(x => x.ProductCategory)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.ProductCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ProductSubCategory)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.ProductSubCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ProductBrand)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.ProductBrandId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ProductTechnology)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.ProductTechnologyId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.ProductImages)
                .WithOne(x => x.Product)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.ProductParametrs)
                .WithOne(x => x.Product)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.ProductDescriptions)
                .WithOne(x => x.Product)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
