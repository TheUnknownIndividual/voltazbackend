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
    public class ProductCategoryLanguageConfiguration : IEntityTypeConfiguration<ProductCategoryLanguage>
    {
        public void Configure(EntityTypeBuilder<ProductCategoryLanguage> builder)
        {
            builder.ToTable("ProductCategoryLanguages");

            builder.HasKey(pcl => pcl.Id);

            builder.Property(pcl => pcl.LanguageCode)
                .IsRequired();

            builder.Property(pcl => pcl.CategoryName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(pcl => pcl.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(pcl => new { pcl.ProductCategoryId, pcl.LanguageCode })
                .IsUnique();
        }
    }
}
