using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration;

public sealed class SolarInverterDatasheetDocumentConfiguration :
    IEntityTypeConfiguration<SolarInverterDatasheetDocument>
{
    public void Configure(EntityTypeBuilder<SolarInverterDatasheetDocument> builder)
    {
        builder.ToTable("SolarInverterDatasheetDocuments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.ParserVersion).HasMaxLength(100).IsRequired();
        builder.Property(x => x.DocumentKind).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ExtractedText).IsRequired();
        builder.Property(x => x.ParsedContentJson).IsRequired();
        builder.HasIndex(x => x.Sha256).IsUnique();
        builder.HasIndex(x => x.SourceUrl);
    }
}
