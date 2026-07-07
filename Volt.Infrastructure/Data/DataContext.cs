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
        public DbSet<CustomerUser> CustomerUsers => Set<CustomerUser>();
        public DbSet<CustomerExternalLogin> CustomerExternalLogins => Set<CustomerExternalLogin>();
        public DbSet<CustomerPasskeyCredential> CustomerPasskeyCredentials => Set<CustomerPasskeyCredential>();
        public DbSet<About> Abouts { get; set; }
        public DbSet<AboutLanguage> AboutLanguages { get; set; }
        public DbSet<Step> Steps { get; set; }
        public DbSet<StepLanguage> StepLanguages { get; set; }
        public DbSet<ServiceManagement> ServiceManagements { get; set; }
        public DbSet<ServiceManagementLanguage> ServiceManagementLanguages { get; set; }
        public DbSet<ApplicationType> ApplicationTypes { get; set; }
        public DbSet<ApplicationTypeLanguage> ApplicationTypeLanguages { get; set; }
        public DbSet<PartnershipType> PartnershipTypes { get; set; }
        public DbSet<PartnershipTypeLanguage> PartnershipTypeLanguages { get; set; }
        public DbSet<PartnershipRequest> PartnershipRequests { get; set; }
        public DbSet<ContactInfo> ContactInfos { get; set; }
        public DbSet<ContactLanguage> ContactLanguages { get; set; }
        public DbSet<PhoneNumber> PhoneNumbers { get; set; }
        public DbSet<EmailAddress> EmailAddresses { get; set; }
        public DbSet<ServiceRequest> ServiceRequests { get; set; }
        public DbSet<ContactRequst> ContactRequsts { get; set; }
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<BlogTranslation> BlogTranslations { get; set; }
        public DbSet<NewsPost> NewsPosts { get; set; }
        public DbSet<NewsPostLanguage> NewsPostLanguages { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectLanguage> ProjectLanguages { get; set; }
        public DbSet<ProjectImage> ProjectImages { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductCategoryLanguage> ProductCategoryLanguages { get; set; }
        public DbSet<ProductSubCategory> ProductSubCategories { get; set; }
        public DbSet<ProductSubCategoryLanguage> ProductSubCategoryLanguages { get; set; }
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<PromotionLanguage> PromotionLanguages { get; set; }
        public DbSet<ProductBrand> ProductBrands { get; set; }
        public DbSet<ProductTechnology> ProductTechnologies { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductParametr> ProductParametrs { get; set; }
        public DbSet<ProductDescription> ProductDescriptions { get; set; }
        public DbSet<ProductDescriptionLanguage> ProductDescriptionLanguages { get; set; }
        public DbSet<ProductPromotion> ProductPromotions { get; set; }
        public DbSet<SolarSalesProject> SolarSalesProjects { get; set; }
        public DbSet<SolarCalculationLog> SolarCalculationLogs { get; set; }
        public DbSet<DocumentSequence> DocumentSequences { get; set; }
        public DbSet<DocumentLog> DocumentLogs { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataContext).Assembly);
            base.OnModelCreating(modelBuilder); 
        }
    }
}
