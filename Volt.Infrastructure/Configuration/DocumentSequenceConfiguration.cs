using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class DocumentSequenceConfiguration : IEntityTypeConfiguration<DocumentSequence>
    {
        public void Configure(EntityTypeBuilder<DocumentSequence> builder)
        {
            builder.ToTable("DocumentSequences");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.DocumentCode)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(x => x.Year)
                .IsRequired();

            builder.Property(x => x.Month)
                .IsRequired();

            builder.Property(x => x.CurrentNumber)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .IsRequired(false);

            builder.HasIndex(x => new { x.DocumentCode, x.Year, x.Month })
                .IsUnique();
        }
    }
}
