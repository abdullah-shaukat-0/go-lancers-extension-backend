using System;

namespace SHMS.Backend.Models
{
    public class CareInstruction
    {
        public int Id { get; set; }

        // The patient this instruction is for
        public int PatientId { get; set; }
        public Patient Patient { get; set; }

        // The doctor who issued the instruction
        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }

        // The nurse assigned to carry out the instruction
        public int NurseId { get; set; }
        public Nurse Nurse { get; set; }

        public string Instructions { get; set; }
        public string Priority { get; set; }   // Low, Medium, High, Critical
        public string Status { get; set; }     // Pending, InProgress, Completed, Cancelled

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Nurse's notes / observations after completing
        public string NurseNotes { get; set; }
    }
}
