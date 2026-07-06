namespace SHMS.Backend.Models
{
    public class Doctor
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string Specialization { get; set; }
        public string RosterSchedule { get; set; } // e.g. "Mon-Fri 9AM-5PM"
        public bool IsAvailable { get; set; }
    }
}
