using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volt.Application.Dtos;

namespace Volt.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomBaseController : ControllerBase
    {
        [NonAction]
        public IActionResult CreateActionResult<T>(ApiResponse<T> result) // T data yox, ApiResponse<T> result
        {
            // Əgər result özü null-dursa (çox nadir hal), boş response qaytar
            if (result == null)
            {
                return new ObjectResult(null) { StatusCode = 204 };
            }

            // Artıq result.SuccessService-də təyin olunub. 
            // Biz sadəcə bu hazır obyekti status kodu ilə birlikdə qaytarırıq.
            return new ObjectResult(result)
            {
                StatusCode = result.Success ? 200 : 400
            };
        }

        // Xəta cavabları üçün (400 Bad Request)
        [NonAction]
        public IActionResult CreateErrorResult(string errorCode, object details = null)
        {
            return new ObjectResult(ApiResponse<object>.ErrorResponse(errorCode, details))
            {
                StatusCode = 400
            };
        }
    }
}
