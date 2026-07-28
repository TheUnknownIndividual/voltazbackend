using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class AuthRefreshTokenConfiguration : IEntityTypeConfiguration<AuthRefreshToken>
    {
        public void Configure(EntityTypeBuilder<AuthRefreshToken> builder)
        {
            builder.ToTable("AuthRefreshTokens");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(x => x.Role)
                .IsRequired();

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.ExpiresAt).IsRequired();
            builder.Property(x => x.ConcurrencyToken).IsRowVersion();

            builder.HasIndex(x => x.TokenHash).IsUnique();
            builder.HasIndex(x => new { x.Role, x.UserId });
            builder.HasIndex(x => x.ExpiresAt);
        }
    }
}
