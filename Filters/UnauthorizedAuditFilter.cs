using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SHMS.Backend.Services;
using System.Threading.Tasks;

namespace SHMS.Backend.Filters
{
    // Registered globally in Startup. Fires after every action.
    // When the result is a 401 or 403, it writes an UNAUTHORIZED_ACCESS_ATTEMPT audit entry.
    public class UnauthorizedAuditFilter : IAsyncActionFilter
    {
        private readonly IAuditService _auditService;

        public UnauthorizedAuditFilter(IAuditService auditService)
        {
            _auditService = auditService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();

            if (executedContext.Result is ObjectResult objectResult &&
                (objectResult.StatusCode == 401 || objectResult.StatusCode == 403))
            {
                var path = context.HttpContext.Request.Path.Value ?? "unknown";
                var method = context.HttpContext.Request.Method;

                await _auditService.LogAsync(new AuditLogEntry
                {
                    Action       = "UNAUTHORIZED_ACCESS_ATTEMPT",
                    ResourceType = "Endpoint",
                    ResourceId   = $"{method} {path}",
                    Details      = $"Access denied to {method} {path}",
                    WasSuccessful = false
                });
            }
        }
    }
}
