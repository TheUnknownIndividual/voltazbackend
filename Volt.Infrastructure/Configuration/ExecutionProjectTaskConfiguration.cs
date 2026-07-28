using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class ExecutionProjectTaskConfiguration : IEntityTypeConfiguration<ExecutionProjectTask>
{
    public void Configure(EntityTypeBuilder<ExecutionProjectTask> builder)
    {
        builder.ToTable("ExecutionProjectTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(180);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(24).HasDefaultValue("Assigned");
        builder.Property(x => x.NotificationStatus).IsRequired().HasMaxLength(32).HasDefaultValue("PendingRecipientLink");
        builder.HasIndex(x => new { x.AssignedAdminUserId, x.Status });
        builder.HasOne(x => x.ExecutionProject).WithMany(x => x.Tasks).HasForeignKey(x => x.ExecutionProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AssignedAdminUser).WithMany().HasForeignKey(x => x.AssignedAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByAdminUser).WithMany().HasForeignKey(x => x.CreatedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
