using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Configuration
{
    public class ApplicationTypeConfiguration : IEntityTypeConfiguration<ApplicationType>
    {
        public void Configure(EntityTypeBuilder<ApplicationType> builder)
        {
            builder.ToTable("ApplicationTypes");

            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.ServiceManagement)
             .WithMany() 
             .HasForeignKey(x => x.ServiceManagementId)
             .OnDelete(DeleteBehavior.SetNull); // Service silinərsə, Id null olsun

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true);
        }
    }
}
