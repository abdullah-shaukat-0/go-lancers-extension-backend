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
        private readonly Services.IAuditService _auditService;

        public BillingController(SHMSDbContext context, Services.IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
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
            await _auditService.LogAsync("PHI_READ", "Billing", patientId?.ToString() ?? "ALL", "Accessed billing records", "Success");
            return Ok(bills);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Doctor")] // PHIPA: Nurses have read-only access to billing records
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

            await _auditService.LogAsync("PHI_WRITE", "Billing", bill.Id.ToString(), $"Manually generated pending bill of amount {model.Amount} for patient {model.PatientId}", "Success");
            return Ok(bill);
        }

        [HttpPut("{id}/pay")]
        [Authorize(Roles = "Admin,Doctor")] // PHIPA: Only Admin/Doctor can mark invoices as paid
        public async Task<IActionResult> PayBill(int id)
        {
            var bill = await _context.Bills.FindAsync(id);
            if (bill == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "Billing", id.ToString(), "Attempted to pay invoice but it was not found", "Failure");
                return NotFound(new { Message = "Invoice not found" });
            }

            bill.PaymentStatus = "Paid";
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("PHI_WRITE", "Billing", id.ToString(), $"Recorded payment for bill {id} of patient {bill.PatientId}", "Success");
            return Ok(bill);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetBillingStats()
        {
            var bills = await _context.Bills.ToListAsync();
            var totalRevenue = bills.Where(b => b.PaymentStatus == "Paid").Sum(b => b.Amount);
            var pendingAmount = bills.Where(b => b.PaymentStatus == "Pending").Sum(b => b.Amount);
            var totalInvoiceCount = bills.Count;

            await _auditService.LogAsync("PHI_READ", "Billing", "STATS", "Viewed aggregate billing statistics dashboard", "Success");
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
