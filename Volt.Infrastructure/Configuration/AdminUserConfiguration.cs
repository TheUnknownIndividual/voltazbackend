using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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

            builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        }
    }
}
