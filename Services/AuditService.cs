using Microsoft.AspNetCore.Http;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SHMS.Backend.Services
{
    public class AuditService : IAuditService
    {
        private readonly SHMSDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(SHMSDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(AuditLogEntry entry)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var principal = httpContext?.User;

            var userId   = entry.OverrideUserId   ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
            var userName = entry.OverrideUserName ?? principal?.FindFirstValue(ClaimTypes.Name)
                        ?? principal?.FindFirstValue("sub")
                        ?? "anonymous";
            var userRole = entry.OverrideUserRole ?? principal?.FindFirstValue(ClaimTypes.Role) ?? "unknown";

            var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

            var log = new AuditLog
            {
                Timestamp     = DateTime.UtcNow,
                UserId        = userId,
                UserName      = userName,
                UserRole      = userRole,
                PatientId     = entry.PatientId,
                Action        = entry.Action,
                ResourceType  = entry.ResourceType,
                ResourceId    = entry.ResourceId,
                Details       = entry.Details,
                IpAddress     = ipAddress,
                WasSuccessful = entry.WasSuccessful
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public Task LogAsync(string action, string resourceType, string resourceId, string details, string status)
        {
            return LogAsync(new AuditLogEntry
            {
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Details = details,
                WasSuccessful = !string.Equals(status, "Failure", StringComparison.OrdinalIgnoreCase)
            });
        }

        public async Task LogAnonymousAsync(string username, string action, string details, string status, string ipAddress)
        {
            var log = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = username ?? "anonymous",
                UserName = username ?? "anonymous",
                UserRole = "anonymous",
                PatientId = null,
                Action = action,
                ResourceType = "Authentication",
                ResourceId = username ?? "anonymous",
                Details = details,
                IpAddress = ipAddress ?? "unknown",
                WasSuccessful = !string.Equals(status, "Failure", StringComparison.OrdinalIgnoreCase)
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
