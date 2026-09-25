
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Volt.API.Services;
using Volt.Application.Configuration;
using Volt.API.Infrastructure.Filters;
using Volt.API.Infrastructure.Localization;
using Volt.API.Middlewares;
using Volt.Application.Dtos;
using Volt.Application.Dtos.SolarInverter;
using Volt.Application.Interfaces;
using Volt.Application.Security;
using Volt.Application.Services;
using Volt.Domain.Interfaces;
using Volt.Infrastructure.Configuration;
using Volt.Infrastructure.Data;
using Volt.Infrastructure.Services;
using Volt.Infrastructure.Services.NewsScraping;
using Volt.Infrastructure.UOW;

namespace Volt.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // HttpClient's default information logs include the Telegram endpoint path,
            // which contains the bot token. Keep this client at warning level; our own
            // warning logs record only safe delivery diagnostics.
            builder.Logging.AddFilter("System.Net.Http.HttpClient.ITelegramTaskNotificationService", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient.IMetaInboxService", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient.IMetaWhatsAppOnboardingService", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient.ProductAiImportProcessor", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient.ContentAiGenerationProcessor", LogLevel.Warning);
            // The Google Places request URL carries the API key as a query parameter;
            // keep this client at warning level for the same reason as Telegram/Meta above.
            builder.Logging.AddFilter("System.Net.Http.HttpClient.IGoogleReviewsService", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient.MetaSocialPublisher", LogLevel.Warning);
            builder.Logging.AddFilter("System.Net.Http.HttpClient.SocialImageService", LogLevel.Warning);

            // This optional file is provisioned only on the production server
            // by the FTP deploy script. It keeps Telegram integration secrets
            // outside source-controlled appsettings files. Environment variables
            // are added afterwards so an IIS/server setting always takes priority.
            builder.Configuration
                .AddJsonFile("telegram.production.json", optional: true, reloadOnChange: false)
                .AddJsonFile("meta-inbox.production.json", optional: true, reloadOnChange: false)
                .AddJsonFile("openai.production.json", optional: true, reloadOnChange: false)
                .AddJsonFile("googlereviews.production.json", optional: true, reloadOnChange: false)
                .AddJsonFile("social-posting.production.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddMemoryCache();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"customer-auth:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 500,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0
                    }));

                options.AddPolicy("admin-auth", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"admin-auth:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 500,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0
                    }));

                options.AddPolicy("public-write", context => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientAddress(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 500,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("public-analytics", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"public-analytics:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("public-agent-draft", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"public-agent-draft:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 4,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("public-agent-confirm", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"public-agent-confirm:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 6,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("public-agent-status", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"public-agent-status:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0
                    }));

                options.AddPolicy("verification-read", context => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientAddress(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 500,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("protected-write", context => RateLimitPartition.GetFixedWindowLimiter(
                    GetClientAddress(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 500,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("product-ai-import", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"product-ai-import:{context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 5,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("content-ai-generate", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"content-ai-generate:{context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("datasheet-preview", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"datasheet-preview:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("marketplace-prepare", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"marketplace-prepare:{context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 30,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("marketplace-payload", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"marketplace-payload:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                options.AddPolicy("meta-webhook", context => RateLimitPartition.GetFixedWindowLimiter(
                    $"meta-webhook:{GetClientAddress(context)}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 3000,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        ApiResponse<object>.ErrorResponse("RATE_LIMITED", "Too many requests. Please try again later."),
                        cancellationToken);
                };
            });

            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("frontend", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            builder.Services.AddDbContext<DataContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<PasswordHelper>();
            builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
            builder.Services.AddScoped<ICustomerAuthService, CustomerAuthService>();
            builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            builder.Services.AddScoped<AuthCookieService>();
            builder.Services.AddScoped<IAdminService, AdminService>();
            builder.Services.AddScoped<IAdminAuditService, AdminAuditService>();
            builder.Services.AddScoped<IAdminAccessService, AdminAccessService>();
            builder.Services.AddScoped<IDocumentVerificationService, DocumentVerificationService>();
            builder.Services.AddScoped<IDocumentVerificationInquiryService, DocumentVerificationInquiryService>();

            builder.Services.Configure<FtpOptions>(builder.Configuration.GetSection("FtpOptions"));

            builder.Services.AddScoped<IAboutService, AboutService>();
            builder.Services.AddScoped<IUploadService, UploadService>();
            builder.Services.AddScoped<IFileService, FileService>();
            builder.Services.AddScoped<IHomeSliderService, HomeSliderService>();
            builder.Services.AddScoped<IStepService, StepService>();
            builder.Services.AddScoped<IServiceManagementService, ServiceManagementService>();
            builder.Services.AddScoped<IApplicationTypeService, ApplicationTypeService>();
            builder.Services.AddScoped<IPartnershipTypeService, PartnershipTypeService>();
            builder.Services.AddScoped<IPartnershipRequestService, PartnershipRequestService>();
            builder.Services.AddScoped<IContactRequstService, ContactRequstService>();
            builder.Services.AddScoped<IContactInfoService, ContactInfoService>();
            builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
            builder.Services.AddScoped<IBlogService, BlogService>();
            builder.Services.AddScoped<INewsPostService, NewsPostService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();
            builder.Services.AddScoped<IAdminProjectTrackerService, AdminProjectTrackerService>();
            builder.Services.AddHttpClient<IProjectAttachmentDocumentExtractor, ProjectAttachmentDocumentExtractor>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            builder.Services.AddScoped<IExecutionProjectService, ExecutionProjectService>();
            builder.Services.AddScoped<IAccountingService, AccountingService>();
            builder.Services.AddScoped<IAdminTelegramConnectionService, AdminTelegramConnectionService>();
            builder.Services.Configure<TelegramBotOptions>(builder.Configuration.GetSection("TelegramBot"));
            builder.Services.AddHttpClient<ITelegramTaskNotificationService, TelegramTaskNotificationService>(client =>
            {
                client.BaseAddress = new Uri("https://api.telegram.org/");
                client.Timeout = TimeSpan.FromSeconds(5);
            });
            builder.Services.Configure<GoogleReviewsOptions>(builder.Configuration.GetSection("GoogleReviews"));
            builder.Services.AddHttpClient<IGoogleReviewsService, GoogleReviewsService>(client =>
            {
                client.BaseAddress = new Uri("https://maps.googleapis.com/");
                client.Timeout = TimeSpan.FromSeconds(8);
            });
            builder.Services.AddScoped<IAcceptLanguageService, AcceptLanguageService>();
            builder.Services.Configure<MetaInboxOptions>(builder.Configuration.GetSection("MetaInbox"));
            builder.Services.AddSingleton<MetaWebhookDiagnostics>();
            builder.Services.AddSingleton<MetaWhatsAppOnboardingSessionStore>();
            builder.Services.AddHttpClient<IMetaInboxService, MetaInboxService>(client =>
            {
                client.BaseAddress = new Uri("https://graph.facebook.com/");
                client.Timeout = TimeSpan.FromSeconds(8);
            });
            builder.Services.AddHttpClient<IMetaInboxHistorySyncService, MetaInboxHistorySyncService>(client =>
            {
                client.BaseAddress = new Uri("https://graph.facebook.com/");
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            builder.Services.AddHttpClient<IMetaWhatsAppOnboardingService, MetaWhatsAppOnboardingService>(client =>
            {
                client.BaseAddress = new Uri("https://graph.facebook.com/");
                client.Timeout = TimeSpan.FromSeconds(15);
            });
            builder.Services.Configure<SeoOptions>(builder.Configuration.GetSection("Seo"));
            builder.Services.AddHttpClient("seo");
            builder.Services.AddScoped<IProductCategoryService, ProductCategoryService>();
            builder.Services.AddScoped<IProductSubCategoryService, ProductSubCategoryService>();
            builder.Services.AddScoped<IProductBrandService, ProductBrandService>();
            builder.Services.AddScoped<IProductTechnologyService, ProductTechnologyService>();
            builder.Services.AddScoped<IProductSearchService, ProductSearchService>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.Configure<ProductAiImportOptions>(builder.Configuration.GetSection("ProductAiImport"));
            builder.Services.AddScoped<ProductDatasheetUploadService>();
            builder.Services.AddHttpClient("product-datasheet-preview", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            builder.Services.AddSingleton<ProductAiImportQueue>();
            builder.Services.AddScoped<ProductAiImportCoordinator>();
            builder.Services.AddHttpClient<ProductAiImportProcessor>(client =>
            {
                client.BaseAddress = new Uri("https://api.openai.com/v1/");
                client.Timeout = Timeout.InfiniteTimeSpan;
            });
            builder.Services.AddHostedService<ProductAiImportBackgroundService>();
            builder.Services.Configure<ContentAiOptions>(builder.Configuration.GetSection("ContentAiGeneration"));
            builder.Services.AddSingleton<ContentAiGenerationQueue>();
            builder.Services.AddScoped<ContentAiGenerationCoordinator>();
            builder.Services.AddHttpClient<ContentAiGenerationProcessor>(client =>
            {
                client.BaseAddress = new Uri("https://api.openai.com/v1/");
                client.Timeout = Timeout.InfiniteTimeSpan;
            });
            builder.Services.AddHostedService<ContentAiGenerationBackgroundService>();
            builder.Services.Configure<RenewableNewsScraperOptions>(builder.Configuration.GetSection("RenewableNewsScraper"));
            builder.Services.AddHttpClient("renewable-news-scraper", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("VoltAzNewsBot/1.0 (+https://volt.az)");
            });
            builder.Services.AddScoped<INewsSourceScraper, MinenergyNewsScraper>();
            builder.Services.AddScoped<INewsSourceScraper, AreaGovNewsScraper>();
            builder.Services.AddScoped<INewsSourceScraper, RenewablesAzNewsScraper>();
            builder.Services.AddScoped<RenewableNewsScraperRunner>();
            builder.Services.AddHostedService<RenewableNewsScraperBackgroundService>();
            builder.Services.Configure<MarketplaceListingsOptions>(builder.Configuration.GetSection("MarketplaceListings"));
            builder.Services.Configure<LalafoListingOptions>(builder.Configuration.GetSection("LalafoListing"));
            builder.Services.Configure<TapAzListingOptions>(builder.Configuration.GetSection("TapAzListing"));
            builder.Services.AddHttpClient<MarketplaceAiClient>(client =>
            {
                client.BaseAddress = new Uri("https://api.openai.com/v1/");
                client.Timeout = Timeout.InfiniteTimeSpan;
            });
            builder.Services.AddScoped<MarketplaceProductLoader>();
            builder.Services.AddScoped<LalafoListingBuilder>();
            builder.Services.AddScoped<TapAzListingBuilder>();
            builder.Services.AddScoped<MarketplaceListingService>();
            builder.Services.Configure<SocialPostingOptions>(builder.Configuration.GetSection("SocialPosting"));
            builder.Services.AddHttpClient<MetaSocialPublisher>(client => client.Timeout = TimeSpan.FromSeconds(45));
            builder.Services.AddHttpClient<SocialImageService>(client => client.Timeout = TimeSpan.FromSeconds(20));
            builder.Services.AddScoped<SocialPostingRunner>();
            builder.Services.AddHostedService<SocialPostingBackgroundService>();
            builder.Services.AddScoped<ISeoFeedService, SeoFeedService>();
            builder.Services.AddSingleton<ISeoSubmissionQueue, SeoSubmissionQueue>();
            builder.Services.AddSingleton<ISeoSubmissionService, SeoSubmissionService>();
            builder.Services.AddHostedService<SeoSubmissionBackgroundService>();
            builder.Services.AddHostedService<PublicAgentDraftCleanupService>();
            builder.Services.AddHostedService<ProjectAttachmentDocumentExtractionService>();
            builder.Services.AddScoped<ISearchService, SearchService>();
            builder.Services.AddScoped<IPromotionService, PromotionService>();
            builder.Services.AddSingleton<IWhatsappInteractionNotificationQueue, WhatsappInteractionNotificationQueue>();
            builder.Services.AddHostedService<WhatsappInteractionTelegramBackgroundService>();
            builder.Services.AddScoped<ISolarAnalyticsService, SolarAnalyticsService>();
            builder.Services.Configure<SolarInverterOcrOptions>(
                builder.Configuration.GetSection("SolarInverterOcr"));
            builder.Services.AddSingleton<ISolarInverterDatasheetParser, SolarInverterDatasheetParser>();
            builder.Services.Configure<SolarInverterPromotionOptions>(
                builder.Configuration.GetSection("SolarInverterPromotion"));
            builder.Services.Configure<SolarInverterQaOptions>(
                builder.Configuration.GetSection("SolarInverterQa"));
            builder.Services.AddScoped<
                ISolarInverterProductionPromotionService,
                SolarInverterProductionPromotionService>();
            builder.Services.AddScoped<
                ISolarInverterDatasheetQaService,
                SolarInverterDatasheetQaService>();
            builder.Services.AddHttpClient<ISolarInverterService, SolarInverterService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IOrderEmailService, OrderEmailService>();

            builder.Services.AddScoped<ITokenService, TokenService>();

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["TokenOptions:Issuer"],
                        ValidAudience = builder.Configuration["TokenOptions:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["TokenOptions:SecurityKey"]))
                    };
                });

            builder.Services.AddAuthorization();
            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ValidateModelAttribute>();
            }).ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });
            var app = builder.Build();

            if (args.Contains("--import-solar-inverter-datasheets", StringComparer.OrdinalIgnoreCase))
            {
                await using var scope = app.Services.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<ISolarInverterService>();
                var commit = args.Contains("--commit", StringComparer.OrdinalIgnoreCase);
                var response = await service.ImportDatasheetsAsync(
                    new SolarInverterDatasheetImportRequest
                    {
                        DryRun = !commit,
                        Force = args.Contains("--force", StringComparer.OrdinalIgnoreCase),
                        RebuildStaging = args.Contains(
                            "--rebuild-staging",
                            StringComparer.OrdinalIgnoreCase)
                    });
                Console.WriteLine(JsonSerializer.Serialize(response));
                return;
            }

            if (args.Contains("--check-solar-inverter-qa", StringComparer.OrdinalIgnoreCase))
            {
                await using var scope = app.Services.CreateAsyncScope();
                var qaService = scope.ServiceProvider
                    .GetRequiredService<ISolarInverterDatasheetQaService>();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                var listResponse = await qaService.GetListAsync(
                    null,
                    null,
                    1,
                    1);
                var detailResponse = listResponse.Success &&
                                     listResponse.Data.Items.Count > 0
                    ? await qaService.GetDetailAsync(
                        listResponse.Data.Items[0].SpecificationId)
                    : null;
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    listResponse.Success,
                    TotalCount = listResponse.Data?.TotalCount,
                    DetailLoaded = detailResponse?.Success ?? false,
                    SourceLinked = detailResponse?.Data?.SourceUrl is not null,
                    ExtractedCharacters =
                        detailResponse?.Data?.OriginalExtractedText.Length ?? 0,
                    SourceImageCount = detailResponse?.Data?.SourceUrls.Count ?? 0,
                    Documents = await dataContext.SolarInverterDatasheetDocuments
                        .AsNoTracking()
                        .GroupBy(x => x.DocumentKind)
                        .Select(x => new { Kind = x.Key, Count = x.Count() })
                        .ToListAsync(),
                    CertificateDocuments =
                        await dataContext.SolarInverterDatasheetDocuments
                            .AsNoTracking()
                            .CountAsync(x =>
                                x.DocumentKind == "certificate" ||
                                x.ExtractedText.Contains("certificate of conformity"))
                }));
                return;
            }

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                await dbContext.Database.MigrateAsync();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseMiddleware<ExceptionMiddleware>();

            app.UseHttpsRedirection();
            
            app.UseCors("frontend");

            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseMiddleware<AdminPageAuthorizationMiddleware>();
            app.UseAuthorization();
            app.UseMiddleware<AdminWriteAuditMiddleware>();

            app.MapControllers();

            await app.RunAsync();
        }

        private static string GetClientAddress(HttpContext context)
        {
            foreach (var headerName in new[] { "CF-Connecting-IP", "X-Forwarded-For", "X-Real-IP" })
            {
                var value = context.Request.Headers[headerName].FirstOrDefault();
                var forwardedAddress = value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (System.Net.IPAddress.TryParse(forwardedAddress, out _))
                {
                    return forwardedAddress;
                }
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
