using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class ExecutionProjectExpenseConfiguration : IEntityTypeConfiguration<ExecutionProjectExpense>
{
    public void Configure(EntityTypeBuilder<ExecutionProjectExpense> builder)
    {
        builder.ToTable("ExecutionProjectExpenses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(80).HasDefaultValue("Müxtəlif xərclər");
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ReceiptUrl).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ExecutionProjectId, x.ExpenseDate });
        builder.HasOne(x => x.ExecutionProject).WithMany(x => x.Expenses).HasForeignKey(x => x.ExecutionProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CreatedByAdminUser).WithMany().HasForeignKey(x => x.CreatedByAdminUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
