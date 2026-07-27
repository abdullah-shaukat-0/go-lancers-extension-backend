using System;

namespace SHMS.Backend.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Who did it
        public string UserId { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
        public string IpAddress { get; set; }

        // What did they do
        public string Action { get; set; } // e.g., "LOGIN_SUCCESS", "LOGIN_FAILURE", "PHI_READ", "PHI_WRITE"
        public string Resource { get; set; } // e.g., "Patients", "Billing", "CareInstructions"
        public string ResourceId { get; set; } // ID of the patient or record affected
        public string Details { get; set; } // Text description of the actions
        public string Outcome { get; set; } // "Success" or "Failure"
    }
}
