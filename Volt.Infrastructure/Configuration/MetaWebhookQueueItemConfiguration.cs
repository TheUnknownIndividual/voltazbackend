#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class MetaWebhookQueueItemConfiguration : IEntityTypeConfiguration<MetaWebhookQueueItem>
    {
        public void Configure(EntityTypeBuilder<MetaWebhookQueueItem> builder)
        {
            builder.ToTable(
                "MetaWebhookQueueItems",
                table => table.HasCheckConstraint(
                    "CK_MetaWebhookQueueItems_Attempts",
                    "[Attempts] >= 0 AND [Attempts] <= 5"));
            builder.HasKey(x => x.Id);
            builder.Property(x => x.PayloadHash).IsRequired().HasMaxLength(64).IsUnicode(false);
            builder.Property(x => x.Payload).HasColumnType("nvarchar(max)");
            builder.Property(x => x.Status).IsRequired().HasMaxLength(20).IsUnicode(false);
            builder.Property(x => x.LastError).HasMaxLength(500);
            builder.HasIndex(x => x.PayloadHash).IsUnique();
            builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
            builder.HasIndex(x => x.CompletedAt);
        }
    }
}
