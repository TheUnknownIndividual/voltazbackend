using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class AdminTelegramConnectionTokenConfiguration : IEntityTypeConfiguration<AdminTelegramConnectionToken>
{
    public void Configure(EntityTypeBuilder<AdminTelegramConnectionToken> builder)
    {
        builder.ToTable("AdminTelegramConnectionTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.AdminUserId, x.ExpiresAt });
        builder.HasOne(x => x.AdminUser).WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
    }
}
