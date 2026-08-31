#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class MetaInboxInternalNoteConfiguration : IEntityTypeConfiguration<MetaInboxInternalNote>
    {
        public void Configure(EntityTypeBuilder<MetaInboxInternalNote> builder)
        {
            builder.ToTable("MetaInboxInternalNotes");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Body).IsRequired().HasMaxLength(2000);
            builder.HasIndex(x => new { x.ConversationId, x.Id });
            builder.HasOne(x => x.Conversation).WithMany(x => x.InternalNotes)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.AuthorAdminUser).WithMany()
                .HasForeignKey(x => x.AuthorAdminUserId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
