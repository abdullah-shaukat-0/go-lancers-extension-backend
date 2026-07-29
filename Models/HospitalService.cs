using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SHMS.Backend.Models
{
    public class HospitalService
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        [JsonIgnore]
        public ICollection<BillItem> BillItems { get; set; }
    }
}
