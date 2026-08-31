using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
    {
        public void Configure(EntityTypeBuilder<ProductCategory> builder)
        {
            builder.ToTable("ProductCategories");

            builder.HasKey(pc => pc.Id);

            builder.Property(pc => pc.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(pc => pc.SeoKey)
                .HasMaxLength(80);

            builder.HasIndex(pc => pc.SeoKey)
                .IsUnique()
                .HasFilter("[SeoKey] IS NOT NULL");

            builder.Property(pc => pc.ShowOnHomePage)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(pc => pc.HomePageDisplayOrder)
                .IsRequired()
                .HasDefaultValue(0);

            builder.HasOne(pc => pc.HomePageProduct)
                .WithMany()
                .HasForeignKey(pc => pc.HomePageProductId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasMany(pc => pc.Languages)
                .WithOne(pcl => pcl.ProductCategory)
                .HasForeignKey(pcl => pcl.ProductCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
