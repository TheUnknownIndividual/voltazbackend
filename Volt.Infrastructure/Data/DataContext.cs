using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volt.Domain.Entities;

namespace Volt.Infrastructure.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }
        public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
        public DbSet<About> Abouts { get; set; }
        public DbSet<AboutImage> AboutImages { get; set; }
        public DbSet<AboutLanguage> AboutLanguages { get; set; }
        public DbSet<Step> Steps { get; set; }
        public DbSet<StepLanguage> StepLanguages { get; set; }
        public DbSet<ServiceManagement> ServiceManagements { get; set; }
        public DbSet<ServiceManagementLanguage> ServiceManagementLanguages { get; set; }
        public DbSet<ApplicationType> ApplicationTypes { get; set; }
        public DbSet<ApplicationTypeLanguage> ApplicationTypeLanguages { get; set; }
        public DbSet<ContactInfo> ContactInfos { get; set; }
        public DbSet<ContactLanguage> ContactLanguages { get; set; }
        public DbSet<PhoneNumber> PhoneNumbers { get; set; }
        public DbSet<EmailAddress> EmailAddresses { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataContext).Assembly);
            base.OnModelCreating(modelBuilder); 
        }
    }
}
