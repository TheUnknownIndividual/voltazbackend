#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class MetaInboxHistorySyncConfiguration : IEntityTypeConfiguration<MetaInboxHistorySync>
    {
        public void Configure(EntityTypeBuilder<MetaInboxHistorySync> builder)
        {
            builder.ToTable(
                "MetaInboxHistorySyncs",
                table =>
                {
                    table.HasCheckConstraint(
                        "CK_MetaInboxHistorySyncs_Progress",
                        "[Progress] >= 0 AND [Progress] <= 100");
                    table.HasCheckConstraint(
                        "CK_MetaInboxHistorySyncs_Phase",
                        "[Phase] IS NULL OR ([Phase] >= 0 AND [Phase] <= 2)");
                    table.HasCheckConstraint(
                        "CK_MetaInboxHistorySyncs_LastChunkOrder",
                        "[LastChunkOrder] IS NULL OR [LastChunkOrder] >= 0");
                });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.PhoneNumberId).IsRequired().HasMaxLength(128);
            builder.Property(x => x.MetaRequestId).HasMaxLength(256);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
            builder.Property(x => x.Progress).HasDefaultValue(0);
            builder.Property(x => x.ErrorCode).HasMaxLength(100);
            builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
            builder.HasIndex(x => x.PhoneNumberId).IsUnique();
        }
    }
}
