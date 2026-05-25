using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TenantPortal.API.Auth
{
    public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
    {
        private const string HeaderName = "x-api-key";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var cfg = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expected = cfg["Integration:ApiKey"];

            if (string.IsNullOrWhiteSpace(expected))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) ||
                string.IsNullOrWhiteSpace(provided))
            {
                context.Result = new UnauthorizedObjectResult(new { errorCode = "API_KEY_REQUIRED" });
                return;
            }

            if (!string.Equals(provided.ToString(), expected, StringComparison.Ordinal))
            {
                context.Result = new UnauthorizedObjectResult(new { errorCode = "INVALID_API_KEY" });
                return;
            }

            await next();
        }
    }
}