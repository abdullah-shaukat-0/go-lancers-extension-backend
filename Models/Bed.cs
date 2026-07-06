namespace SHMS.Backend.Models
{
    public class Bed
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; }
        public string WardType { get; set; } // ICU, General, Pediatric, Surgical
        public bool IsOccupied { get; set; }
        public int? PatientId { get; set; }
        public Patient Patient { get; set; }
    }
}
