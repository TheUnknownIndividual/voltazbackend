using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class PublicAgentContactDraftConfiguration : IEntityTypeConfiguration<PublicAgentContactDraft>
{
    public void Configure(EntityTypeBuilder<PublicAgentContactDraft> builder)
    {
        builder.ToTable("PublicAgentContactDrafts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PublicId).IsRequired();
        builder.Property(x => x.AccessTokenHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Surname).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Phone).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
    }
}
