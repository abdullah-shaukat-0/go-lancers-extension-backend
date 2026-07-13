using System.Text.Json.Serialization;

namespace SHMS.Backend.Models
{
    public class BillItem
    {
        public int Id { get; set; }
        public int BillId { get; set; }
        [JsonIgnore]
        public Bill Bill { get; set; }
        public int? HospitalServiceId { get; set; }
        public HospitalService HospitalService { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}
