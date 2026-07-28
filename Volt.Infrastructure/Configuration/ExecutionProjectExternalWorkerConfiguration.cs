using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class ExecutionProjectExternalWorkerConfiguration : IEntityTypeConfiguration<ExecutionProjectExternalWorker>
{
    public void Configure(EntityTypeBuilder<ExecutionProjectExternalWorker> builder)
    {
        builder.ToTable("ExecutionProjectExternalWorkers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.AmountPaid).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.ExecutionProjectId, x.StartDate });
        builder.HasOne(x => x.ExecutionProject).WithMany(x => x.ExternalWorkers).HasForeignKey(x => x.ExecutionProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedByAdminUser).WithMany().HasForeignKey(x => x.CreatedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
