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
    public sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
    {
        public void Configure(EntityTypeBuilder<AdminUser> builder)
        {
            builder.ToTable("AdminUsers");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Is_active)
            .HasDefaultValue(true)
            .IsRequired();
        }
    }
}
