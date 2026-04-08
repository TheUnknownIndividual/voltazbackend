
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using Volt.API.Infrastructure.Filters;
using Volt.API.Middlewares;
using Volt.Application.Interfaces;
using Volt.Application.Security;
using Volt.Application.Services;
using Volt.Domain.Interfaces;
using Volt.Infrastructure.Configuration;
using Volt.Infrastructure.Data;
using Volt.Infrastructure.Services;
using Volt.Infrastructure.UOW;

namespace Volt.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddDbContext<DataContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<PasswordHelper>();
            builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
            builder.Services.AddScoped<IAdminService, AdminService>();

            builder.Services.Configure<FtpOptions>(builder.Configuration.GetSection("FtpOptions"));

            builder.Services.AddScoped<IAboutService, AboutService>();
            builder.Services.AddScoped<IUploadService, UploadService>();
            builder.Services.AddScoped<IFileService, FileService>();
            builder.Services.AddScoped<IStepService, StepService>();
            builder.Services.AddScoped<IServiceManagementService, ServiceManagementService>();
            builder.Services.AddScoped<IApplicationTypeService, ApplicationTypeService>();

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

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseMiddleware<ExceptionMiddleware>();

            app.UseHttpsRedirection();
            
            app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
