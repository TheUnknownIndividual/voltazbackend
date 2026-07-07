using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class CustomerExternalLoginConfiguration : IEntityTypeConfiguration<CustomerExternalLogin>
    {
        public void Configure(EntityTypeBuilder<CustomerExternalLogin> builder)
        {
            builder.ToTable("CustomerExternalLogins");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Provider).IsRequired().HasMaxLength(40);
            builder.Property(x => x.ProviderSubject).IsRequired().HasMaxLength(240);
            builder.Property(x => x.Email).IsRequired().HasMaxLength(180);
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.LastLoginAt).IsRequired();

            builder.HasIndex(x => new { x.Provider, x.ProviderSubject }).IsUnique();
            builder.HasIndex(x => x.Email);

            builder.HasOne(x => x.CustomerUser)
                .WithMany(x => x.ExternalLogins)
                .HasForeignKey(x => x.CustomerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
