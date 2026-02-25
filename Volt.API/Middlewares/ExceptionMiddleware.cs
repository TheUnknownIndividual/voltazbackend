using System.Net;
using Volt.Application.Dtos;
using Volt.Domain.Common;

namespace Volt.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gözlənilməz xəta baş verdi.");

                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500

            // Sənin prototipindəki ErrorResponse formatına uyğunlaşdırırıq
            var response = ApiResponse<object>.ErrorResponse(
                ErrorCode.SERVER_ERROR,
                ex.Message // Real layihədə bura "Internal Server Error" yazıb, ex.Message-i logda saxlamaq daha təhlükəsizdir
            );

            return context.Response.WriteAsJsonAsync(response);
        }
    }
}
