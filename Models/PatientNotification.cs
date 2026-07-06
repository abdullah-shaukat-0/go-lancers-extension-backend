using System;

namespace SHMS.Backend.Models
{
    public class PatientNotification
    {
        public int Id { get; set; }

        // Recipient
        public int PatientId { get; set; }
        public Patient Patient { get; set; }

        // Sender info
        public string SenderId { get; set; }        // ApplicationUser.Id of the sender
        public string SenderName { get; set; }      // Full name for display
        public string SenderRole { get; set; }      // Doctor or Nurse

        public string Subject { get; set; }
        public string Message { get; set; }

        // Type: Precaution | Checkup | Recovery | General
        public string NotificationType { get; set; }

        public bool IsRead { get; set; }
        public DateTime SentAt { get; set; }

        // Optional scheduled delivery (null = sent immediately)
        public DateTime? ScheduledFor { get; set; }

        // Whether the simulated email has been "dispatched"
        public bool IsEmailSent { get; set; }
    }
}
