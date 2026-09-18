using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class RenewableNewsScraperRunStateConfiguration : IEntityTypeConfiguration<RenewableNewsScraperRunState>
    {
        public void Configure(EntityTypeBuilder<RenewableNewsScraperRunState> builder)
        {
            builder.ToTable("RenewableNewsScraperRunState");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();
        }
    }
}
