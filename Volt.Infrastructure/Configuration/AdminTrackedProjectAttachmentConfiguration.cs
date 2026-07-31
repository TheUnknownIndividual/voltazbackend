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
            builder.Property(x => x.Tag).IsRequired().HasMaxLength(40).HasDefaultValue("Qiymət təklifi");
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.DocumentText).HasColumnType("nvarchar(max)");
            builder.Property(x => x.DocumentExtractionStatus).IsRequired().HasMaxLength(32).HasDefaultValue("NotRequired");
            builder.Property(x => x.DocumentExtractionError).HasMaxLength(300);
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.HasIndex(x => new { x.DocumentExtractionStatus, x.CreatedAt });
        }
    }
}
