using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class ServiceCategorySettingConfiguration : IEntityTypeConfiguration<ServiceCategorySetting>
    {
        public void Configure(EntityTypeBuilder<ServiceCategorySetting> builder)
        {
            builder.ToTable("ServiceCategorySettings");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Category).IsRequired();
            builder.Property(x => x.IsReadMoreEnabled).IsRequired();
            builder.Property(x => x.UpdatedAt).IsRequired();
            builder.HasIndex(x => x.Category).IsUnique();
        }
    }
}
