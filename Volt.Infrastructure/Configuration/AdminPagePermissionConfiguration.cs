using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class AdminPagePermissionConfiguration : IEntityTypeConfiguration<AdminPagePermission>
    {
        public void Configure(EntityTypeBuilder<AdminPagePermission> builder)
        {
            builder.ToTable("AdminPagePermissions");
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => new { x.AdminUserId, x.Page }).IsUnique();
            builder.HasOne(x => x.AdminUser).WithMany(x => x.PagePermissions)
                .HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
