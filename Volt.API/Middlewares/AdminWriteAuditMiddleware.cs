using System.Security.Claims;
using Volt.Application.Interfaces;
using Volt.Domain.Enums;

namespace Volt.API.Middlewares
{
    /// <summary>Records successful and failed authenticated admin mutations without retaining request bodies.</summary>
    public sealed class AdminWriteAuditMiddleware
    {
        private readonly RequestDelegate _next;
        public AdminWriteAuditMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IAdminAuditService audit)
        {
            await _next(context);

            if (context.User.Identity?.IsAuthenticated != true ||
                !string.Equals(context.User.FindFirstValue(ClaimTypes.Role), Role.Admin.ToString(), StringComparison.Ordinal) ||
                !int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ||
                !(HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
                  HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method)))
                return;

            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            // These use domain-specific audit labels with document/user targets.
            if (path.StartsWith("/api/admins") || path.StartsWith("/api/document-verifications") || path.StartsWith("/api/document-verification-inquiries") || path.StartsWith("/api/solaranalytics"))
                return;

            try
            {
                var target = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                await audit.WriteAsync(
                    adminId,
                    context.User.FindFirstValue(ClaimTypes.Name),
                    $"{context.Request.Method}_ADMIN_ACTION",
                    target.Length > 1 ? target[1] : "api",
                    target.Length > 2 ? string.Join('/', target.Skip(2)) : null,
                    null,
                    context.Response.StatusCode is >= 200 and < 400,
                    context.RequestAborted);
            }
            catch
            {
                // An audit-storage incident must not change the completed admin action response.
            }
        }
    }
}
