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
    public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
    {
        public void Configure(EntityTypeBuilder<Promotion> builder)
        {
            builder.ToTable("Promotions");

            builder.HasKey(pc => pc.Id);

            builder.Property(pc => pc.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasMany(pc => pc.Languages)
                .WithOne(pcl => pcl.Promotion)
                .HasForeignKey(pcl => pcl.PromotionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
