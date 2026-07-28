using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class ProjectAttachmentConfiguration : IEntityTypeConfiguration<ProjectAttachment>
    {
        public void Configure(EntityTypeBuilder<ProjectAttachment> builder)
        {
            builder.ToTable("ProjectAttachments");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.FilePath)
                .IsRequired();

            builder.Property(x => x.Label)
                .HasMaxLength(120);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);
        }
    }
}
