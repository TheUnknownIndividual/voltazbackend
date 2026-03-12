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
    public class AboutImageConfiguration : IEntityTypeConfiguration<AboutImage>
    {
        public void Configure(EntityTypeBuilder<AboutImage> builder)
        {
            builder.ToTable("AboutImages");

            builder.HasKey(ai => ai.Id);

            builder.Property(ai => ai.ImagePath)
                    .IsRequired();

            builder.Property(ai => ai.IsActive)
                .IsRequired()
                .HasDefaultValue(true);
        }
    }
}
