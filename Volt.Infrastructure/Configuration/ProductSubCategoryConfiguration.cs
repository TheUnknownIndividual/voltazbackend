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
    public class ProductSubCategoryConfiguration : IEntityTypeConfiguration<ProductSubCategory>
    {
        public void Configure(EntityTypeBuilder<ProductSubCategory> builder)
        {
            builder.ToTable("ProductSubCategories");

            builder.HasKey(pc => pc.Id);

            builder.Property(pc => pc.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasOne(pc => pc.ProductCategory)
                .WithMany(pc => pc.SubCategories)
                .HasForeignKey(pc => pc.ProductCategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(pc => pc.Languages)
                .WithOne(pcl => pcl.ProductSubCategory)
                .HasForeignKey(pcl => pcl.ProductSubCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
