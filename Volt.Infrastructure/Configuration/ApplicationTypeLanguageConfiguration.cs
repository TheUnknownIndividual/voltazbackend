using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class ApplicationTypeLanguageConfiguration : IEntityTypeConfiguration<ApplicationTypeLanguage>
    {
        public void Configure(EntityTypeBuilder<ApplicationTypeLanguage> builder)
        {
            builder.ToTable("ApplicationTypeLanguages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired();

            builder.Property(x => x.LanguageCode)
                .IsRequired();

            // ApplicationType ilə əlaqə
            builder.HasOne(x => x.ApplicationType)
                .WithMany(x => x.Languages)
                .HasForeignKey(x => x.ApplicationTypeId)
                .OnDelete(DeleteBehavior.Cascade); // Əsas tip silinərsə, tərcümələri də silinsin

            // UNİKAL İNDEKS: Bir tətbiq növünün eyni dildə yalnız bir adı ola bilər
            builder.HasIndex(x => new { x.ApplicationTypeId, x.LanguageCode })
                .IsUnique();

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true);
        }
    }
}
