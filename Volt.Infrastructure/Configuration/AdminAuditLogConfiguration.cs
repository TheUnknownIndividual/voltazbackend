using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
    {
        public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
        {
            builder.ToTable("AdminAuditLogs");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ActorUsername).HasMaxLength(100);
            builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
            builder.Property(x => x.TargetType).HasMaxLength(100);
            builder.Property(x => x.TargetId).HasMaxLength(100);
            builder.Property(x => x.Summary).HasMaxLength(1000);
            builder.HasIndex(x => new { x.AdminUserId, x.CreatedAt });
            builder.HasIndex(x => x.CreatedAt);
            builder.HasOne(x => x.AdminUser).WithMany(x => x.AuditLogs)
                .HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
