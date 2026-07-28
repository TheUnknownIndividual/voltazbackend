using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class SolarInverterSpecificationConfiguration : IEntityTypeConfiguration<SolarInverterSpecification>
{
    public void Configure(EntityTypeBuilder<SolarInverterSpecification> builder)
    {
        builder.ToTable("SolarInverterSpecifications");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TechnicalPower).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ModelLabel).HasMaxLength(240).IsRequired();
        builder.Property(x => x.SystemType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Phase).HasMaxLength(16).IsRequired();
        builder.Property(x => x.NominalAcKw).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MaxDcKw).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MpptRange).HasMaxLength(64);
        builder.Property(x => x.MaxInputCurrent).HasMaxLength(64);
        builder.Property(x => x.Manufacturer).HasMaxLength(120);
        builder.Property(x => x.RegionalGridVersion).HasMaxLength(120);
        builder.Property(x => x.DatasheetUrl).HasMaxLength(1000);
        builder.Property(x => x.DatasheetRevision).HasMaxLength(120);
        builder.Property(x => x.SupportedGridVoltageRange).HasMaxLength(120);
        builder.Property(x => x.SupportedFrequencyRange).HasMaxLength(80);
        builder.Property(x => x.AcSpdClass).HasMaxLength(80);
        builder.Property(x => x.DcSpdClass).HasMaxLength(80);
        builder.Property(x => x.RequiredGridCertifications).HasMaxLength(1000);
        builder.Property(x => x.QaStatus).HasMaxLength(24).HasDefaultValue("not-confirmed");
        builder.Property(x => x.QaNotes).HasMaxLength(2000);
        builder.Property(x => x.ProductionPromotionMessage).HasMaxLength(1000);
        builder.Property(x => x.MaxAcApparentPowerKva).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MaxAcOutputCurrentA).HasColumnType("decimal(18,3)");
        builder.Property(x => x.NominalAcVoltageV).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MaxOperatingCurrentPerStringA).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MaxOperatingCurrentPerMpptA).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MaxShortCircuitCurrentPerStringA).HasColumnType("decimal(18,3)");
        builder.Property(x => x.MaxShortCircuitCurrentPerMpptA).HasColumnType("decimal(18,3)");
        builder.Property(x => x.WarrantyYears).HasDefaultValue(5);
        builder.Property(x => x.IsEligible).HasDefaultValue(true);

        builder.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProductId, x.TechnicalPower }).IsUnique();
        builder.HasIndex(x => new { x.SystemType, x.Phase, x.IsEligible });
        builder.HasIndex(x => new { x.QaStatus, x.QaDoneAt });
    }
}
