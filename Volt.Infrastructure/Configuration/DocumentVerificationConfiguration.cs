using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class DocumentVerificationConfiguration : IEntityTypeConfiguration<DocumentVerification>
    {
        public void Configure(EntityTypeBuilder<DocumentVerification> builder)
        {
            builder.ToTable("DocumentVerifications");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.PublicToken).IsRequired().HasMaxLength(128);
            builder.Property(x => x.DocumentNumber).IsRequired().HasMaxLength(80);
            builder.Property(x => x.DocumentCode).IsRequired().HasMaxLength(10);
            builder.Property(x => x.IssuerDisplayName).IsRequired().HasMaxLength(160);
            builder.Property(x => x.RevocationReason).HasMaxLength(1000);
            builder.HasIndex(x => x.PublicToken).IsUnique();
            builder.HasIndex(x => x.DocumentLogId).IsUnique();
            builder.HasIndex(x => x.ExpiresAt);
            builder.HasOne(x => x.DocumentLog).WithOne(x => x.Verification)
                .HasForeignKey<DocumentVerification>(x => x.DocumentLogId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.RevokedByAdminUser).WithMany()
                .HasForeignKey(x => x.RevokedByAdminUserId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
