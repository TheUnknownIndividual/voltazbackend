using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class DocumentVerificationInquiryConfiguration : IEntityTypeConfiguration<DocumentVerificationInquiry>
    {
        public void Configure(EntityTypeBuilder<DocumentVerificationInquiry> builder)
        {
            builder.ToTable("DocumentVerificationInquiries");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Type).IsRequired().HasMaxLength(32);
            builder.Property(x => x.Comment).IsRequired().HasMaxLength(2000);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.UpdatedAt).IsRequired();
            builder.HasIndex(x => new { x.Status, x.CreatedAt });
            builder.HasIndex(x => x.AssignedAdminUserId);
            builder.HasIndex(x => x.AdminTrackedProjectId);
            builder.HasOne(x => x.DocumentVerification).WithMany(x => x.Inquiries)
                .HasForeignKey(x => x.DocumentVerificationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.AdminTrackedProject).WithMany()
                .HasForeignKey(x => x.AdminTrackedProjectId).OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.AssignedAdminUser).WithMany()
                .HasForeignKey(x => x.AssignedAdminUserId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}
