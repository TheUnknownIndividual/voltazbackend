using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class AdminUserConfiguration : IEntityTypeConfiguration<AdminUser>
    {
        public void Configure(EntityTypeBuilder<AdminUser> builder)
        {
            builder.ToTable("AdminUsers");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Username).IsRequired().HasMaxLength(100);
            builder.Property(e => e.DisplayName).HasMaxLength(160).HasDefaultValue("");
            builder.Property(e => e.IsSuperAdmin).HasDefaultValue(false);
            builder.Property(e => e.CanDeleteProjects).HasDefaultValue(false);
            builder.Property(e => e.CanEditProjects).HasDefaultValue(false);
            builder.Property(e => e.CanApproveWarehouseMovements).HasDefaultValue(false);
            builder.Property(e => e.IsStakeholder).HasDefaultValue(false);
            builder.Property(e => e.TelegramChatId);
            builder.Property(e => e.ReceivesYoxlaNotifications).HasDefaultValue(false);
            builder.Property(e => e.ReceivesQiymetlendirmeNotifications).HasDefaultValue(false);
            builder.Property(e => e.MonthlySalary).HasColumnType("decimal(18,2)");
            builder.Property(e => e.EmploymentStartDate).HasColumnType("date");
            builder.Property(e => e.SalaryPaymentDate).HasColumnType("date");
            builder.HasIndex(e => e.TelegramChatId).IsUnique().HasFilter("[TelegramChatId] IS NOT NULL");
            builder.HasIndex(e => e.Username).IsUnique();

            builder.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();
        }
    }
}
