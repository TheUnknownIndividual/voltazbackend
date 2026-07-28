using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public sealed class AdminTrackedProjectConfiguration : IEntityTypeConfiguration<AdminTrackedProject>
    {
        public void Configure(EntityTypeBuilder<AdminTrackedProject> builder)
        {
            builder.ToTable("AdminTrackedProjects");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Location).HasMaxLength(300);
            builder.Property(x => x.PersonName).HasMaxLength(160);
            builder.Property(x => x.PhoneNumber).HasMaxLength(40);
            builder.Property(x => x.CurrentStatus).HasMaxLength(80);
            builder.Property(x => x.SmallNote).HasMaxLength(140);
            builder.Property(x => x.Description).HasColumnType("nvarchar(max)");
            builder.Property(x => x.StakeholderApprovalStatus).IsRequired().HasMaxLength(32).HasDefaultValue("NotRequired");
            builder.Property(x => x.OfferPrice).HasColumnType("decimal(18,2)");
            builder.Property(x => x.IncludesAdv).HasDefaultValue(true);
            builder.Property(x => x.SystemType).HasColumnType("tinyint");
            builder.Property(x => x.IsActive).HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).IsRequired();

            builder.HasMany(x => x.Offers)
                .WithOne(x => x.AdminTrackedProject)
                .HasForeignKey(x => x.AdminTrackedProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Attachments)
                .WithOne(x => x.AdminTrackedProject)
                .HasForeignKey(x => x.AdminTrackedProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.CreatedAt);
            builder.HasIndex(x => x.CurrentStatus);
            builder.HasIndex(x => x.StakeholderApprovalStatus);
        }
    }
}
