using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class ProjectLanguageConfiguration : IEntityTypeConfiguration<ProjectLanguage>
    {
        public void Configure(EntityTypeBuilder<ProjectLanguage> builder)
        {
            builder.ToTable("ProjectLanguages");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.LanguageCode)
                .IsRequired();

            builder.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.Description)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(x => new { x.ProjectId, x.LanguageCode })
                .IsUnique();
        }
    }
}
