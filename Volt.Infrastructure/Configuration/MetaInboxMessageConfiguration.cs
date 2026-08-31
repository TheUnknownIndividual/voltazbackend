#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class MetaInboxMessageConfiguration : IEntityTypeConfiguration<MetaInboxMessage>
    {
        public void Configure(EntityTypeBuilder<MetaInboxMessage> builder)
        {
            builder.ToTable("MetaInboxMessages");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ExternalMessageId).HasMaxLength(256);
            builder.Property(x => x.Text).HasMaxLength(4000);
            builder.Property(x => x.AttachmentsJson).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(x => x.DeliveryStatus).IsRequired().HasMaxLength(32);
            builder.HasIndex(x => new { x.ConversationId, x.ExternalMessageId })
                .IsUnique().HasFilter("[ExternalMessageId] IS NOT NULL");
            builder.HasIndex(x => new { x.ConversationId, x.Id });
            builder.HasOne(x => x.Conversation).WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.SentByAdminUser).WithMany()
                .HasForeignKey(x => x.SentByAdminUserId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
