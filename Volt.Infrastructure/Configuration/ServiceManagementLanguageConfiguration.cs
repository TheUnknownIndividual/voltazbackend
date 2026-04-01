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
    public class ServiceManagementLanguageConfiguration : IEntityTypeConfiguration<ServiceManagementLanguage>
    {
        public void Configure(EntityTypeBuilder<ServiceManagementLanguage> builder)
        {
            builder.ToTable("ServiceManagementLanguages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.LanguageCode)
                .IsRequired();

            builder.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.Description)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(x => new { x.ServiceMagamentId, x.LanguageCode })
                .IsUnique();
        }
    }
}
