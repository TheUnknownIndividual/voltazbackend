using Microsoft.AspNetCore.Mvc.Filters;
using System.ComponentModel.DataAnnotations;
using Volt.API.Infrastructure.Results;

namespace Volt.API.Infrastructure.Filters
{
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.ModelState.IsValid)
            {
                var validationResults = context.ModelState
                    .Where(ms => ms.Value.Errors.Count > 0)
                    .SelectMany(ms => ms.Value.Errors
                        .Select(err => new ValidationResult(
                            err.ErrorMessage, new[] { ms.Key })))
                    .ToList();

                context.Result = new ValidationFailedResult(validationResults);
            }
        }
    }
}
