#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class MetaInboxWhatsAppContactConfiguration : IEntityTypeConfiguration<MetaInboxWhatsAppContact>
    {
        public void Configure(EntityTypeBuilder<MetaInboxWhatsAppContact> builder)
        {
            builder.ToTable("MetaInboxWhatsAppContacts");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.PhoneNumberId).IsRequired().HasMaxLength(128);
            builder.Property(x => x.ParticipantExternalId).IsRequired().HasMaxLength(128);
            builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
            builder.HasIndex(x => new { x.PhoneNumberId, x.ParticipantExternalId }).IsUnique();
        }
    }
}
