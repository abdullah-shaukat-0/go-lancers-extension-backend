using System;

namespace SHMS.Backend.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string MedicalHistory { get; set; }
        public string BloodGroup { get; set; }
        public string Gender { get; set; }
        public DateTime DateOfBirth { get; set; }
    }
}
