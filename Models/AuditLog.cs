using System;

namespace SHMS.Backend.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        public DateTime Timestamp { get; set; }

        public string UserId { get; set; }

        public string UserName { get; set; }

        public string UserRole { get; set; }

        // Null for non-patient-specific actions (e.g. LOGIN, VIEW_BED_LIST)
        public int? PatientId { get; set; }

        // Enum-style string: VIEW_PATIENT_PROFILE, BOOK_APPOINTMENT, etc.
        public string Action { get; set; }

        // The type of entity accessed: Patient, Appointment, Bed, Bill, etc.
        public string ResourceType { get; set; }

        // The ID of the accessed entity (as string to accommodate non-integer IDs)
        public string ResourceId { get; set; }

        // Human-readable context about what happened
        public string Details { get; set; }

        public string IpAddress { get; set; }

        // False when an unauthorized access attempt was blocked
        public bool WasSuccessful { get; set; }
    }
}
