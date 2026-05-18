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

            builder.HasMany(pc => pc.Languages)
                .WithOne(pcl => pcl.ProductCategory)
                .HasForeignKey(pcl => pcl.ProductCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
