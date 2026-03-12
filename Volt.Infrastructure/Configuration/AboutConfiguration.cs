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
    public class AboutConfiguration : IEntityTypeConfiguration<About>
    {
        public void Configure(EntityTypeBuilder<About> builder)
        {
            builder.ToTable("Abouts");

            builder.HasKey(a => a.Id);

            builder.Property(a => a.Position)
                .IsRequired();

            builder.Property(a => a.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(a => a.CreatedAt)
                .IsRequired();

            builder.Property(a => a.UpdatedAt)
                .IsRequired(false);

            builder.HasMany(a => a.Languages)
                .WithOne(ad => ad.About)
                .HasForeignKey(ad => ad.AboutId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(a => a.Images)
                .WithOne(ai => ai.About)
                .HasForeignKey(ai => ai.AboutId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
