using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class ExecutionProjectBoqItemConfiguration : IEntityTypeConfiguration<ExecutionProjectBoqItem>
{
    public void Configure(EntityTypeBuilder<ExecutionProjectBoqItem> builder)
    {
        builder.ToTable("ExecutionProjectBoqItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ItemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Unit).IsRequired().HasMaxLength(24);
        builder.Property(x => x.PlannedQuantity).HasColumnType("decimal(18,3)");
        builder.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne(x => x.ExecutionProject).WithMany(x => x.BoqItems).HasForeignKey(x => x.ExecutionProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}
