using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class AdminTrackedProjectAttachmentConfiguration : IEntityTypeConfiguration<AdminTrackedProjectAttachment>
    {
        public void Configure(EntityTypeBuilder<AdminTrackedProjectAttachment> builder)
        {
            builder.ToTable("AdminTrackedProjectAttachments");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.FileName).IsRequired().HasMaxLength(260);
            builder.Property(x => x.FilePath).IsRequired().HasColumnType("nvarchar(max)");
            builder.Property(x => x.Label).HasMaxLength(120);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
        }
    }
}
