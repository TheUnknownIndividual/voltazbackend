using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class StakeholderApprovalRequestConfiguration : IEntityTypeConfiguration<StakeholderApprovalRequest>
{
    public void Configure(EntityTypeBuilder<StakeholderApprovalRequest> builder)
    {
        builder.ToTable("StakeholderApprovalRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EnvironmentScope).IsRequired().HasMaxLength(8);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.CancellationReason).HasMaxLength(250);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.AdminTrackedProjectId, x.Status });
        builder.HasOne(x => x.AdminTrackedProject).WithMany().HasForeignKey(x => x.AdminTrackedProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
