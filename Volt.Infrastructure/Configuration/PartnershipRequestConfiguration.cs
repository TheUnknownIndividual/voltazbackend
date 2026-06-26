using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class PartnershipRequestConfiguration : IEntityTypeConfiguration<PartnershipRequest>
    {
        public void Configure(EntityTypeBuilder<PartnershipRequest> builder)
        {
            builder.ToTable("PartnershipRequests");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.CompanyName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.CompanyPerson)
                .IsRequired()
                .HasMaxLength(150);

            builder.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.PhoneNumber)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.Message)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(x => x.Status)
                .IsRequired()
                .HasDefaultValue((byte)1);

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .IsRequired(false);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasOne(x => x.PartnershipType)
                .WithMany(x => x.PartnershipRequests)
                .HasForeignKey(x => x.PartnershipTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
