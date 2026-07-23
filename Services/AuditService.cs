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
    }
}
