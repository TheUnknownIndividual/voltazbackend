using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class StakeholderApprovalRecipientConfiguration : IEntityTypeConfiguration<StakeholderApprovalRecipient>
{
    public void Configure(EntityTypeBuilder<StakeholderApprovalRecipient> builder)
    {
        builder.ToTable("StakeholderApprovalRecipients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DeliveryStatus).IsRequired().HasMaxLength(32);
        builder.Property(x => x.FailureStatus).HasMaxLength(80);
        builder.HasIndex(x => new { x.StakeholderApprovalRequestId, x.AdminUserId }).IsUnique();
        builder.HasOne(x => x.StakeholderApprovalRequest).WithMany(x => x.Recipients).HasForeignKey(x => x.StakeholderApprovalRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}
