using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class ExecutionProjectConfiguration : IEntityTypeConfiguration<ExecutionProject>
{
    public void Configure(EntityTypeBuilder<ExecutionProject> builder)
    {
        builder.ToTable("ExecutionProjects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(40).HasDefaultValue("Aktiv");
        builder.Property(x => x.ArchiveReason).HasMaxLength(250);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.ProjectId).IsUnique().HasFilter("[ProjectId] IS NOT NULL");
        builder.HasIndex(x => x.AdminTrackedProjectId).IsUnique().HasFilter("[AdminTrackedProjectId] IS NOT NULL");
        builder.HasIndex(x => x.ProjectManagerAdminUserId);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AdminTrackedProject).WithMany().HasForeignKey(x => x.AdminTrackedProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ProjectManager).WithMany().HasForeignKey(x => x.ProjectManagerAdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
