#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class MetaInboxConversationConfiguration : IEntityTypeConfiguration<MetaInboxConversation>
    {
        public void Configure(EntityTypeBuilder<MetaInboxConversation> builder)
        {
            builder.ToTable("MetaInboxConversations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.AccountExternalId).IsRequired().HasMaxLength(128);
            builder.Property(x => x.ParticipantExternalId).IsRequired().HasMaxLength(128);
            builder.Property(x => x.ParticipantDisplayName).IsRequired().HasMaxLength(200);
            builder.Property(x => x.ParticipantAvatarUrl).HasMaxLength(2048);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(24);
            builder.Property(x => x.LastMessagePreview).IsRequired().HasMaxLength(500);
            builder.HasIndex(x => new { x.Channel, x.AccountExternalId, x.ParticipantExternalId }).IsUnique();
            builder.HasIndex(x => new { x.Status, x.LastMessageAt });
            builder.HasIndex(x => x.AssignedAdminUserId);
            builder.HasOne(x => x.AssignedAdminUser).WithMany()
                .HasForeignKey(x => x.AssignedAdminUserId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
