using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class SocialPostConfiguration : IEntityTypeConfiguration<SocialPost>
    {
        public void Configure(EntityTypeBuilder<SocialPost> builder)
        {
            builder.ToTable("SocialPosts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.SourceType).IsRequired().HasMaxLength(16);
            builder.Property(x => x.Platform).IsRequired().HasMaxLength(16);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(16);
            builder.Property(x => x.Caption).HasMaxLength(2500);
            builder.Property(x => x.ImageUrl).HasMaxLength(1000);
            builder.Property(x => x.LinkUrl).HasMaxLength(500);
            builder.Property(x => x.TopicKey).HasMaxLength(120);
            builder.Property(x => x.RejectReason).HasMaxLength(500);
            builder.Property(x => x.ExternalId).HasMaxLength(64);
            builder.Property(x => x.PermalinkUrl).HasMaxLength(500);
            builder.HasIndex(x => new { x.Platform, x.SourceType, x.SourceId }).IsUnique();
            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => x.Status);
        }
    }

    public sealed class SocialPostingStateConfiguration : IEntityTypeConfiguration<SocialPostingState>
    {
        public void Configure(EntityTypeBuilder<SocialPostingState> builder)
        {
            builder.ToTable("SocialPostingState");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
        }
    }
}
