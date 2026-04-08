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
    public class ContactInfoConfiguration : IEntityTypeConfiguration<ContactInfo>
    {
        public void Configure(EntityTypeBuilder<ContactInfo> builder)
        {
            builder.ToTable("ContactInfos");
            builder.HasKey(c => c.Id);

            // Bir əlaqənin çoxlu nömrəsi ola bilər
            builder.HasMany(c => c.PhoneNumbers)
                .WithOne(p => p.ContactInfo)
                .HasForeignKey(p => p.ContactInfoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bir əlaqənin çoxlu maili ola bilər
            builder.HasMany(c => c.EmailAddresses)
                .WithOne(e => e.ContactInfo)
                .HasForeignKey(e => e.ContactInfoId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bir əlaqənin 4 dildə tərcüməsi (Address və WorkingHours)
            builder.HasMany(c => c.Languages)
                .WithOne(l => l.ContactInfo)
                .HasForeignKey(l => l.ContactInfoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
