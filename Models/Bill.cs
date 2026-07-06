using System;

namespace SHMS.Backend.Models
{
    public class Bill
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public Patient Patient { get; set; }
        public int? AppointmentId { get; set; }
        public Appointment Appointment { get; set; }
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; } // Pending, Paid
        public DateTime DateGenerated { get; set; }
    }
}
