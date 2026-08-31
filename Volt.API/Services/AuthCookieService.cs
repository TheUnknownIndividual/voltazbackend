using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Volt.Domain.Enums;

namespace Volt.API.Services
{
    public sealed class AuthCookieService
    {
        public const string RefreshCookieName = "volt_refresh_token";

        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public AuthCookieService(IWebHostEnvironment environment, IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public void SetRefreshCookie(HttpResponse response, string refreshToken, Role? role = null)
        {
            response.Cookies.Append(RefreshCookieName, refreshToken, CreateOptions(role));
        }

        public void ClearRefreshCookie(HttpResponse response, Role? role = null)
        {
            response.Cookies.Delete(RefreshCookieName, CreateOptions(role));
        }

        private CookieOptions CreateOptions(Role? role)
            => new()
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/api/Auth",
                MaxAge = TimeSpan.FromDays(GetCookieLifetimeDays(role)),
                IsEssential = true,
            };

        private int GetCookieLifetimeDays(Role? role)
        {
            if (role == Role.Admin &&
                int.TryParse(_configuration["TokenOptions:AdminRefreshTokenDays"], out var adminDays))
            {
                return Math.Max(1, adminDays);
            }

            return int.TryParse(_configuration["TokenOptions:RefreshTokenDays"], out var days)
                ? Math.Max(1, days)
                : 30;
        }
    }
}
