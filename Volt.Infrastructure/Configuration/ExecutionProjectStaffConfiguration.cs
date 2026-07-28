using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class ExecutionProjectStaffConfiguration : IEntityTypeConfiguration<ExecutionProjectStaff>
{
    public void Configure(EntityTypeBuilder<ExecutionProjectStaff> builder)
    {
        builder.ToTable("ExecutionProjectStaff");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RoleName).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => new { x.ExecutionProjectId, x.StartDate });
        builder.HasIndex(x => new { x.ExecutionProjectId, x.AdminUserId }).IsUnique();
        builder.HasOne(x => x.ExecutionProject).WithMany(x => x.Staff).HasForeignKey(x => x.ExecutionProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AdminUser).WithMany().HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
