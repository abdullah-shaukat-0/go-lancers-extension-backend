namespace SHMS.Backend.Models
{
    public class Nurse
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string Department { get; set; } // e.g. "ICU", "General Ward", "Pediatrics"
        public string Shift { get; set; }       // e.g. "Morning", "Evening", "Night"
        public bool IsAvailable { get; set; }
    }
}
