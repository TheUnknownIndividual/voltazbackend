using System.Security.Claims;
using Volt.Application.Interfaces;
using Volt.Domain.Enums;

namespace Volt.API.Middlewares
{
    /// <summary>
    /// Keeps API authorization authoritative for delegated admin accounts. UI navigation is
    /// only a convenience; every mapped admin write/read still passes this check.
    /// </summary>
    public sealed class AdminPageAuthorizationMiddleware
    {
        private readonly RequestDelegate _next;

        public AdminPageAuthorizationMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IAdminAccessService access)
        {
            if (context.User.Identity?.IsAuthenticated != true ||
                !string.Equals(context.User.FindFirstValue(ClaimTypes.Role), Role.Admin.ToString(), StringComparison.Ordinal) ||
                !int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) ||
                !TryResolve(context.Request, out var page, out var needsProjectDelete, out var needsProjectEdit))
            {
                await _next(context);
                return;
            }

            var allowed = needsProjectDelete
                ? await access.CanDeleteProjectsAsync(adminId, context.RequestAborted)
                : needsProjectEdit
                    ? await access.CanEditProjectsAsync(adminId, context.RequestAborted)
                    : await access.HasPageAsync(adminId, page, context.RequestAborted);

            if (allowed)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = "FORBIDDEN",
                message = needsProjectDelete
                    ? "Project deletion is not enabled for this account."
                    : needsProjectEdit
                        ? "Project editing is not enabled for this account."
                        : "This admin account is not permitted to access this page."
            });
        }

        private static bool TryResolve(HttpRequest request, out AdminPage page, out bool needsProjectDelete, out bool needsProjectEdit)
        {
            page = default;
            needsProjectDelete = false;
            needsProjectEdit = false;
            var path = request.Path.Value?.ToLowerInvariant() ?? string.Empty;

            // The management endpoints do their own full-admin check in the controller.
            if (path.StartsWith("/api/admins") || path.StartsWith("/api/document-verifications"))
                return false;

            if (path.StartsWith("/api/solaranalytics/dashboard")) { page = AdminPage.Analytics; return true; }
            if (path.StartsWith("/api/solaranalytics")) { page = AdminPage.SolarCalculator; return true; }
            if (path.StartsWith("/api/solarinverters/datasheets/qa")) { page = AdminPage.SolarInverterQa; return true; }
            if (path.StartsWith("/api/solarinverters")) { page = AdminPage.SolarCalculator; return true; }
            if (path.StartsWith("/api/orders")) { page = AdminPage.Orders; return true; }
            if (path.StartsWith("/api/contactrequsts")) { page = AdminPage.Requests; return true; }
            if (path.StartsWith("/api/document-verification-inquiries")) { page = AdminPage.Requests; return true; }
            if (path.StartsWith("/api/servicerequests")) { page = AdminPage.ServiceRequests; return true; }
            if (path.StartsWith("/api/partnershiprequests")) { page = AdminPage.PartnershipRequests; return true; }
            if (path.StartsWith("/api/adminprojecttracker"))
            {
                page = AdminPage.ProjectTracker;
                needsProjectDelete = HttpMethods.IsDelete(request.Method);
                needsProjectEdit = HttpMethods.IsPut(request.Method);
                return true;
            }
            if (path.StartsWith("/api/executionprojects") && path.Contains("/external-workers")) { page = AdminPage.Accounting; return true; }
            if (path.StartsWith("/api/executionprojects")) { page = AdminPage.ExecutionProjects; return true; }
            if (path.StartsWith("/api/accounting")) { page = AdminPage.Accounting; return true; }
            if (path.StartsWith("/api/projects"))
            {
                // Anonymous/customer public reads bypass this middleware; an admin token must have the page grant.
                page = AdminPage.Projects;
                needsProjectDelete = HttpMethods.IsDelete(request.Method);
                return true;
            }
            if (path.StartsWith("/api/products") || path.StartsWith("/api/productcategories") ||
                path.StartsWith("/api/productsubcategories") || path.StartsWith("/api/productbrands") ||
                path.StartsWith("/api/producttechnologies")) { page = AdminPage.Warehouse; return true; }
            if (path.StartsWith("/api/abouts") || path.StartsWith("/api/servicesmanagement") ||
                path.StartsWith("/api/steps") || path.StartsWith("/api/applicationtypes") ||
                path.StartsWith("/api/partnershiptypes") || path.StartsWith("/api/blogs") ||
                path.StartsWith("/api/newsposts") || path.StartsWith("/api/promotions") ||
                path.StartsWith("/api/contactinfos") || path.StartsWith("/api/uploads")) { page = AdminPage.Settings; return true; }

            return false;
        }
    }
}
