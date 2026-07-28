using Microsoft.AspNetCore.Http;

namespace Volt.API.Services
{
    public sealed class AuthCookieService
    {
        public const string RefreshCookieName = "volt_refresh_token";

        private readonly IWebHostEnvironment _environment;

        public AuthCookieService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public void SetRefreshCookie(HttpResponse response, string refreshToken)
        {
            response.Cookies.Append(RefreshCookieName, refreshToken, CreateOptions());
        }

        public void ClearRefreshCookie(HttpResponse response)
        {
            response.Cookies.Delete(RefreshCookieName, CreateOptions());
        }

        private CookieOptions CreateOptions()
            => new()
            {
                HttpOnly = true,
                Secure = !_environment.IsDevelopment(),
                SameSite = SameSiteMode.Lax,
                Path = "/api/Auth",
                MaxAge = TimeSpan.FromDays(30),
                IsEssential = true,
            };
    }
}
