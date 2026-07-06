using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BillingController : ControllerBase
    {
        private readonly SHMSDbContext _context;

        public BillingController(SHMSDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBills(int? patientId)
        {
            var query = _context.Bills
                .Include(b => b.Patient).ThenInclude(p => p.User)
                .Include(b => b.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
                .AsQueryable();

            if (patientId.HasValue)
                query = query.Where(b => b.PatientId == patientId.Value);

            var bills = await query.OrderByDescending(b => b.DateGenerated).ToListAsync();
            return Ok(bills);
        }

        [HttpPost]
        public async Task<IActionResult> GenerateManualBill([FromBody] ManualBillModel model)
        {
            var bill = new Bill
            {
                PatientId = model.PatientId,
                AppointmentId = null,
                Amount = model.Amount,
                PaymentStatus = "Pending",
                DateGenerated = DateTime.UtcNow
            };

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            return Ok(bill);
        }

        [HttpPut("{id}/pay")]
        public async Task<IActionResult> PayBill(int id)
        {
            var bill = await _context.Bills.FindAsync(id);
            if (bill == null) return NotFound(new { Message = "Invoice not found" });

            bill.PaymentStatus = "Paid";
            await _context.SaveChangesAsync();

            return Ok(bill);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetBillingStats()
        {
            var bills = await _context.Bills.ToListAsync();
            var totalRevenue = bills.Where(b => b.PaymentStatus == "Paid").Sum(b => b.Amount);
            var pendingAmount = bills.Where(b => b.PaymentStatus == "Pending").Sum(b => b.Amount);
            var totalInvoiceCount = bills.Count;

            return Ok(new
            {
                TotalRevenue = totalRevenue,
                PendingAmount = pendingAmount,
                TotalInvoiceCount = totalInvoiceCount
            });
        }
    }

    public class ManualBillModel
    {
        public int PatientId { get; set; }
        public decimal Amount { get; set; }
    }
}
