using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;
using Volt.Domain.Enums;

namespace Volt.Infrastructure.Configuration
{
    public sealed class CustomerUserConfiguration : IEntityTypeConfiguration<CustomerUser>
    {
        public void Configure(EntityTypeBuilder<CustomerUser> builder)
        {
            builder.ToTable("CustomerUsers");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.FirstName).IsRequired().HasMaxLength(80);
            builder.Property(x => x.LastName).IsRequired().HasMaxLength(80);
            builder.Property(x => x.Email).IsRequired().HasMaxLength(180);
            builder.Property(x => x.Phone).HasMaxLength(40);
            builder.Property(x => x.Address).HasMaxLength(300);
            builder.Property(x => x.PasswordHash).IsRequired(false);
            builder.Property(x => x.PasswordSalt).IsRequired(false);
            builder.Property(x => x.Role).HasDefaultValue(Role.Customer).HasSentinel((Role)0).IsRequired();
            builder.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
            builder.Property(x => x.CreatedAt).IsRequired();

            builder.HasIndex(x => x.Email).IsUnique();
            builder.HasIndex(x => x.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
        }
    }
}
