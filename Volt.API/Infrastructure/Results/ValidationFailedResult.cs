using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Volt.Application.Dtos;
using Volt.Domain.Common;

namespace Volt.API.Infrastructure.Results
{
    public class ValidationFailedResult : ObjectResult
    {
        public ValidationFailedResult(IEnumerable<ValidationResult> validationResults) 
            : base(ApiResponse<object>.ErrorResponse(
            ErrorCode.VALIDATION_ERROR,
            validationResults
                .SelectMany(vr => vr.MemberNames.Select(m => new
                {
                    Field = m,
                    Message = vr.ErrorMessage
                })).GroupBy(e => e.Field)
    .ToDictionary(
                g => g.Key, g => g.Select(e => e.Message).ToArray()
                )))  
        {
                StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}
