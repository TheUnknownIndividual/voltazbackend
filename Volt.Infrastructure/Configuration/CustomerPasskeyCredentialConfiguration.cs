using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class CustomerPasskeyCredentialConfiguration : IEntityTypeConfiguration<CustomerPasskeyCredential>
    {
        public void Configure(EntityTypeBuilder<CustomerPasskeyCredential> builder)
        {
            builder.ToTable("CustomerPasskeyCredentials");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CredentialId).IsRequired().HasMaxLength(1024);
            builder.Property(x => x.CredentialIdBase64Url).IsRequired().HasMaxLength(1400);
            builder.Property(x => x.PublicKey).IsRequired();
            builder.Property(x => x.UserHandle).IsRequired().HasMaxLength(128);
            builder.Property(x => x.CredType).IsRequired().HasMaxLength(40);
            builder.Property(x => x.SignatureCounter).IsRequired();
            builder.Property(x => x.AaGuid).IsRequired();
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();

            builder.HasIndex(x => x.CredentialId).IsUnique();
            builder.HasIndex(x => x.CredentialIdBase64Url).IsUnique();
            builder.HasIndex(x => x.UserHandle);

            builder.HasOne(x => x.CustomerUser)
                .WithMany(x => x.PasskeyCredentials)
                .HasForeignKey(x => x.CustomerUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
