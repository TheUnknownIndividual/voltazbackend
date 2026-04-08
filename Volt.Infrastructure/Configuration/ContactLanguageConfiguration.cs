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
    internal class ContactLanguageConfiguration : IEntityTypeConfiguration<ContactLanguage>
    {
        public void Configure(EntityTypeBuilder<ContactLanguage> builder)
        {
            builder.ToTable("ContactLanguages");
            builder.HasKey(l => l.Id);

            builder.Property(l => l.Address)
                .IsRequired();

            builder.Property(l => l.WorkingHoursDescription)
                .IsRequired();

            builder.Property(l => l.LanguageCode)
                .IsRequired();

            // UNIQUE CONSTRAINT: Bir Contact üçün eyni dil kodu təkrar oluna bilməz
            builder.HasIndex(l => new { l.ContactInfoId, l.LanguageCode }).IsUnique();
        }
    }
}
