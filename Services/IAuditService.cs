using System.Threading.Tasks;

namespace SHMS.Backend.Services
{
    public class AuditLogEntry
    {
        public int? PatientId { get; set; }
        public string Action { get; set; }
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
        public string Details { get; set; }
        public bool WasSuccessful { get; set; } = true;
        // Set these in AuthController where the JWT principal is not yet established
        public string OverrideUserId { get; set; }
        public string OverrideUserName { get; set; }
        public string OverrideUserRole { get; set; }
    }

    public interface IAuditService
    {
        Task LogAsync(AuditLogEntry entry);
        Task LogAsync(string action, string resourceType, string resourceId, string details, string status);
        Task LogAnonymousAsync(string username, string action, string details, string status, string ipAddress);
    }
}
