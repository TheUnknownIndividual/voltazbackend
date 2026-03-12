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
    public class AboutLanguageConfiguration : IEntityTypeConfiguration<AboutLanguage>
    {
        public void Configure(EntityTypeBuilder<AboutLanguage> builder)
        {
            builder.ToTable("AboutLanguages");

            builder.HasKey(al => al.Id);

            builder.Property(al => al.Title)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(al => al.Description)
                .IsRequired();

            builder.Property(al => al.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(al => new { al.AboutId, al.LanguageCode })
                .IsUnique();
        }
    }
}
