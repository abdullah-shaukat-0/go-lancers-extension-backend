using Microsoft.AspNetCore.Http;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SHMS.Backend.Services
{
    public interface IAuditService
    {
        Task LogAsync(string action, string resource, string resourceId, string details, string outcome);
        Task LogAnonymousAsync(string username, string action, string details, string outcome, string ipAddress);
    }

    public class AuditService : IAuditService
    {
        private readonly SHMSDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(SHMSDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string resource, string resourceId, string details, string outcome)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            string userId = "System";
            string username = "System";
            string role = "System";
            string ipAddress = "0.0.0.0";

            if (httpContext != null)
            {
                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
                
                var user = httpContext.User;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown";
                    username = user.Identity.Name ?? "Unknown";
                    role = user.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
                }
            }

            var log = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                Username = username,
                Role = role,
                IpAddress = ipAddress,
                Action = action,
                Resource = resource,
                ResourceId = resourceId,
                Details = details,
                Outcome = outcome
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogAnonymousAsync(string username, string action, string details, string outcome, string ipAddress)
        {
            var log = new AuditLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = "Anonymous",
                Username = username ?? "Anonymous",
                Role = "None",
                IpAddress = ipAddress ?? "0.0.0.0",
                Action = action,
                Resource = "Authentication",
                ResourceId = null,
                Details = details,
                Outcome = outcome
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
