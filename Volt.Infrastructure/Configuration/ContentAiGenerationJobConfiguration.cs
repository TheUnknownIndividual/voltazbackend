using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class ContentAiGenerationJobConfiguration : IEntityTypeConfiguration<ContentAiGenerationJob>
    {
        public void Configure(EntityTypeBuilder<ContentAiGenerationJob> builder)
        {
            builder.ToTable("ContentAiGenerationJobs");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Origin).IsRequired().HasMaxLength(16);
            builder.Property(x => x.ContentType).IsRequired().HasMaxLength(16);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
            builder.Property(x => x.RequestJson).IsRequired();
            builder.Property(x => x.DraftJson);
            builder.Property(x => x.ErrorCode).HasMaxLength(100);
            builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
            builder.HasIndex(x => new { x.CreatedByAdminId, x.Status });
            builder.HasIndex(x => x.ExpiresAt);
            builder.HasIndex(x => x.ContentType);
        }
    }
}
