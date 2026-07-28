using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class ExecutionWarehouseMovementConfiguration : IEntityTypeConfiguration<ExecutionWarehouseMovement>
{
    public void Configure(EntityTypeBuilder<ExecutionWarehouseMovement> builder)
    {
        builder.ToTable("ExecutionWarehouseMovements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Unit).IsRequired().HasMaxLength(24);
        builder.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Direction).IsRequired().HasMaxLength(3);
        builder.Property(x => x.ApprovalStatus).IsRequired().HasMaxLength(16).HasDefaultValue("Pending");
        builder.Property(x => x.ApprovalNote).HasMaxLength(500);
        builder.HasIndex(x => new { x.ExecutionProjectId, x.MovedAt });
        builder.HasOne(x => x.ExecutionProject).WithMany(x => x.WarehouseMovements).HasForeignKey(x => x.ExecutionProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
        // Product deletion already cascades to ProductParametrs; SET NULL here would create a second SQL Server cascade path.
        builder.HasOne(x => x.ProductParametr).WithMany().HasForeignKey(x => x.ProductParametrId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
