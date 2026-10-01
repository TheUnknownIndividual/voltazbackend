using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class LinkedInCredentialConfiguration : IEntityTypeConfiguration<LinkedInCredential>
    {
        public void Configure(EntityTypeBuilder<LinkedInCredential> builder)
        {
            builder.ToTable("LinkedInCredential");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.OrganizationUrn).HasMaxLength(100);
            builder.Property(x => x.AccessToken).HasMaxLength(4000);
            builder.Property(x => x.RefreshToken).HasMaxLength(4000);
        }
    }
}
