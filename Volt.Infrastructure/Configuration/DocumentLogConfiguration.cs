using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class DocumentLogConfiguration : IEntityTypeConfiguration<DocumentLog>
    {
        public void Configure(EntityTypeBuilder<DocumentLog> builder)
        {
            builder.ToTable("DocumentLogs");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.DocumentCode)
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(x => x.DocumentNumber)
                .IsRequired()
                .HasMaxLength(40);

            builder.Property(x => x.PayloadJson)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.HasOne(x => x.SolarSalesProject)
                .WithMany(x => x.DocumentLogs)
                .HasForeignKey(x => x.SolarSalesProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.SolarCalculationLog)
                .WithMany(x => x.DocumentLogs)
                .HasForeignKey(x => x.SolarCalculationLogId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne(x => x.AdminUser)
                .WithMany()
                .HasForeignKey(x => x.AdminUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasIndex(x => x.DocumentNumber)
                .IsUnique();
            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => x.SolarSalesProjectId);
        }
    }
}
