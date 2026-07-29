using System;

namespace SHMS.Backend.Models
{
    public class Expense
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
