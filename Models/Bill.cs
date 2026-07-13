using System;
using System.Collections.Generic;

namespace SHMS.Backend.Models
{
    public class Bill
    {
        public int Id { get; set; }
        public string InvoiceNumber { get; set; }
        public int PatientId { get; set; }
        public Patient Patient { get; set; }
        public int? AppointmentId { get; set; }
        public Appointment Appointment { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Amount { get; set; }
        public string PaymentStatus { get; set; } // Pending, Paid, Cancelled
        public string Notes { get; set; }
        public DateTime DateGenerated { get; set; }
        public DateTime? DatePaid { get; set; }
        public ICollection<BillItem> Items { get; set; }
    }
}
