using System;

namespace SHMS.Backend.Models
{
    public class PatientCorrectionRequest
    {
        public int Id { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; }

        public string FieldName { get; set; }
        public string CurrentValue { get; set; }
        public string RequestedValue { get; set; }
        public string Reason { get; set; }

        public string Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public string ReviewedByUserId { get; set; }
        public ApplicationUser ReviewedByUser { get; set; }
        public string ReviewNote { get; set; }
    }
}
