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
        public IActionResult CreateActionResult<T>(T data)
        {
            if (data == null)
            {
                return new ObjectResult(ApiResponse<T>.SuccessResponse(default))
                {
                    StatusCode = 200
                };

            }

            return new ObjectResult(ApiResponse<T>.SuccessResponse(data))
            {
                StatusCode = 200
            };
        }

        [NonAction]
        public IActionResult CreateErrorResult(string errorCode, object details = null)
        {
            return new ObjectResult(ApiResponse<string>.ErrorResponse(errorCode, details))
            {
                StatusCode = 400
            };
        }
    }
}
