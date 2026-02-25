using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Volt.Application.Dtos
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public ErrorResponse<object> Error { get; set; }

        public static ApiResponse<T> SuccessResponse(T data) =>
            new ApiResponse<T> { Success = true, Data = data , Error = null};

        public static ApiResponse<T> ErrorResponse(string code, object details) =>
            new ApiResponse<T> { Success = false, Data = default, Error = new ErrorResponse<object> { Code = code, Details = details } };
    }
}
