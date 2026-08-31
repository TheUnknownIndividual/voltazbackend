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
    public class ServiceManagementConfiguration : IEntityTypeConfiguration<ServiceManagement>
    {
        
        public void Configure(EntityTypeBuilder<ServiceManagement> builder)
        {
            builder.ToTable("ServiceManagements");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.Category)
                .IsRequired()
                .HasDefaultValue(Volt.Domain.Enums.ServiceCategory.Population);

            builder.Property(x => x.ReadMoreUrl)
                .HasMaxLength(500)
                .IsRequired(false);

            builder.Property(x => x.DetailPageSlug)
                .HasMaxLength(160)
                .IsRequired(false);

            builder.Property(x => x.BannerImageUrl)
                .HasMaxLength(1000)
                .IsRequired(false);

            builder.HasIndex(x => x.DetailPageSlug)
                .IsUnique()
                .HasFilter("[DetailPageSlug] IS NOT NULL");

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .IsRequired(false);

            builder.HasMany(x => x.Languages)
                .WithOne(x => x.ServiceManagement)
                .HasForeignKey(x => x.ServiceMagamentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
