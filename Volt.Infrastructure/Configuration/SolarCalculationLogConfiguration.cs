using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class SolarCalculationLogConfiguration : IEntityTypeConfiguration<SolarCalculationLog>
    {
        public void Configure(EntityTypeBuilder<SolarCalculationLog> builder)
        {
            builder.ToTable("SolarCalculationLogs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Source)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.EventType)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.Language)
                .HasMaxLength(10);

            builder.Property(x => x.SessionId)
                .HasMaxLength(100);

            builder.Property(x => x.PayloadJson)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasOne(x => x.SolarSalesProject)
                .WithMany(x => x.CalculationLogs)
                .HasForeignKey(x => x.SolarSalesProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.AdminUser)
                .WithMany()
                .HasForeignKey(x => x.AdminUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.AdminTrackedProject)
                .WithMany()
                .HasForeignKey(x => x.AdminTrackedProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => new { x.Source, x.EventType, x.CreatedAt });
            builder.HasIndex(x => x.SolarSalesProjectId);
            builder.HasIndex(x => x.AdminTrackedProjectId);
        }
    }
}
