using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using SHMS.Backend.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BillingController : ControllerBase
    {
        private const string PendingStatus = "Pending";
        private const string PaidStatus = "Paid";
        private const string CancelledStatus = "Cancelled";

        private readonly SHMSDbContext _context;
        private readonly IAuditService _auditService;

        public BillingController(SHMSDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBills(int? patientId, string status)
        {
            if (User.IsInRole("Patient"))
            {
                var currentPatientId = await GetCurrentPatientIdAsync();
                if (!currentPatientId.HasValue)
                    return Forbid();

                if (patientId.HasValue && patientId.Value != currentPatientId.Value)
                    return Forbid();

                patientId = currentPatientId.Value;
            }

            var query = _context.Bills
                .Include(b => b.Patient).ThenInclude(p => p.User)
                .Include(b => b.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
                .Include(b => b.Items).ThenInclude(i => i.HospitalService)
                .AsQueryable();

            if (patientId.HasValue)
                query = query.Where(b => b.PatientId == patientId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(b => b.PaymentStatus == status);

            var bills = await query
                .OrderByDescending(b => b.DateGenerated)
                .Select(b => new
                {
                    b.Id,
                    b.InvoiceNumber,
                    b.PatientId,
                    b.AppointmentId,
                    b.Subtotal,
                    b.DiscountAmount,
                    b.TaxAmount,
                    b.Amount,
                    b.PaymentStatus,
                    b.Notes,
                    b.DateGenerated,
                    b.DatePaid,
                    Patient = new
                    {
                        b.Patient.Id,
                        User = new
                        {
                            b.Patient.User.FullName
                        }
                    },
                    Appointment = b.Appointment == null ? null : new
                    {
                        b.Appointment.Id,
                        Doctor = new
                        {
                            User = new
                            {
                                b.Appointment.Doctor.User.FullName
                            }
                        }
                    },
                    Items = b.Items.Select(i => new
                    {
                        i.Id,
                        i.Description,
                        i.Quantity,
                        i.UnitPrice,
                        i.LineTotal,
                        HospitalService = i.HospitalService == null ? null : new
                        {
                            i.HospitalService.Id,
                            i.HospitalService.Name,
                            i.HospitalService.Category,
                            i.HospitalService.Price,
                            i.HospitalService.IsActive
                        }
                    }).ToList()
                })
                .ToListAsync();

            await _auditService.LogAsync(new AuditLogEntry
            {
                PatientId    = patientId,
                Action       = "VIEW_BILLING",
                ResourceType = "Bill",
                ResourceId   = patientId.HasValue ? patientId.ToString() : "all",
                Details      = $"Retrieved {bills.Count} billing record(s)"
            });

            return Ok(bills);
        }

        [HttpGet("patient/{patientId}")]
        public Task<IActionResult> GetPatientBills(int patientId, string status)
        {
            return GetAllBills(patientId, status);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Doctor")] // PHIPA: Nurses have read-only access to billing records
        public async Task<IActionResult> GenerateManualBill([FromBody] ManualBillModel model)
        {
            if (model == null || model.PatientId <= 0 || model.Amount <= 0)
                return BadRequest(new { Message = "PatientId and a positive Amount are required." });

            var patientExists = await _context.Patients.AnyAsync(p => p.Id == model.PatientId);
            if (!patientExists) return NotFound(new { Message = "Patient not found" });

            var bill = new Bill
            {
                PatientId = model.PatientId,
                AppointmentId = null,
                InvoiceNumber = await GenerateInvoiceNumberAsync(),
                Subtotal = model.Amount,
                DiscountAmount = 0,
                TaxAmount = 0,
                Amount = model.Amount,
                PaymentStatus = PendingStatus,
                Notes = model.Notes,
                DateGenerated = DateTime.UtcNow,
                Items = new List<BillItem>
                {
                    new BillItem
                    {
                        Description = string.IsNullOrWhiteSpace(model.Description) ? "Manual billing item" : model.Description,
                        Quantity = 1,
                        UnitPrice = model.Amount,
                        LineTotal = model.Amount
                    }
                }
            };

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(new AuditLogEntry
            {
                PatientId    = model.PatientId,
                Action       = "CREATE_INVOICE",
                ResourceType = "Bill",
                ResourceId   = bill.Id.ToString(),
                Details      = $"Manual invoice of ${model.Amount} generated for patient #{model.PatientId}"
            });

            return Ok(bill);
        }

        [HttpPut("{id}/pay")]
        [Authorize(Roles = "Admin,Doctor,Patient")]
        public async Task<IActionResult> PayBill(int id)
        {
            var bill = await _context.Bills.FindAsync(id);
            if (bill == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "Billing", id.ToString(), "Attempted to pay invoice but it was not found", "Failure");
                return NotFound(new { Message = "Invoice not found" });
            }

            if (User.IsInRole("Patient"))
            {
                var currentPatientId = await GetCurrentPatientIdAsync();
                if (!currentPatientId.HasValue || bill.PatientId != currentPatientId.Value)
                    return Forbid();
            }

            return await UpdateBillStatusInternal(bill, PaidStatus);
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin,Doctor,Nurse")]
        public async Task<IActionResult> UpdateBillStatus(int id, [FromBody] BillStatusUpdateModel model)
        {
            var bill = await _context.Bills.FindAsync(id);
            if (bill == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "Billing", id.ToString(), "Attempted to pay invoice but it was not found", "Failure");
                return NotFound(new { Message = "Invoice not found" });
            }

            if (!IsValidPaymentStatus(model?.PaymentStatus))
                return BadRequest(new { Message = "PaymentStatus must be Pending, Paid, or Cancelled." });

            return await UpdateBillStatusInternal(bill, model.PaymentStatus);
        }

        private async Task<IActionResult> UpdateBillStatusInternal(Bill bill, string paymentStatus)
        {
            bill.PaymentStatus = paymentStatus;
            bill.DatePaid = paymentStatus == PaidStatus ? DateTime.UtcNow : (DateTime?)null;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(new AuditLogEntry
            {
                PatientId    = bill.PatientId,
                Action       = "PAY_INVOICE",
                ResourceType = "Bill",
                ResourceId   = bill.Id.ToString(),
                Details      = $"Invoice #{bill.Id} marked as {paymentStatus.ToLower()} for patient #{bill.PatientId}"
            });

            return Ok(bill);
        }

        [HttpGet("services")]
        public async Task<IActionResult> GetServices()
        {
            var services = await _context.HospitalServices
                .OrderBy(s => s.Category)
                .ThenBy(s => s.Name)
                .ToListAsync();

            return Ok(services);
        }

        [HttpPost("services")]
        public async Task<IActionResult> CreateService([FromBody] HospitalServiceModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name) || model.Price < 0)
                return BadRequest(new { Message = "Name and non-negative Price are required." });

            var service = new HospitalService
            {
                Name = model.Name,
                Category = string.IsNullOrWhiteSpace(model.Category) ? "General" : model.Category,
                Price = model.Price,
                IsActive = true
            };

            _context.HospitalServices.Add(service);
            await _context.SaveChangesAsync();

            return Ok(service);
        }

        [HttpGet("expenses")]
        public async Task<IActionResult> GetExpenses()
        {
            var expenses = await _context.Expenses
                .OrderByDescending(e => e.ExpenseDate)
                .ThenByDescending(e => e.Id)
                .ToListAsync();

            return Ok(expenses);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetBillingStats()
        {
            var bills = await _context.Bills.ToListAsync();
            var expenses = await _context.Expenses.ToListAsync();
            var totalRevenue = bills.Where(b => b.PaymentStatus == PaidStatus).Sum(b => b.Amount);
            var pendingAmount = bills.Where(b => b.PaymentStatus == PendingStatus).Sum(b => b.Amount);
            var cancelledAmount = bills.Where(b => b.PaymentStatus == CancelledStatus).Sum(b => b.Amount);
            var totalExpenses = expenses.Sum(e => e.Amount);

            await _auditService.LogAsync("PHI_READ", "Billing", "STATS", "Viewed aggregate billing statistics dashboard", "Success");
            return Ok(new
            {
                TotalRevenue = totalRevenue,
                PendingAmount = pendingAmount,
                CancelledAmount = cancelledAmount,
                TotalExpenses = totalExpenses,
                NetIncome = totalRevenue - totalExpenses,
                TotalInvoiceCount = bills.Count,
                PaidInvoiceCount = bills.Count(b => b.PaymentStatus == PaidStatus),
                PendingInvoiceCount = bills.Count(b => b.PaymentStatus == PendingStatus)
            });
        }

        private async Task<string> GenerateInvoiceNumberAsync()
        {
            var nextId = await _context.Bills.CountAsync() + 1;
            return $"INV-{DateTime.UtcNow:yyyyMMdd}-{nextId:D5}";
        }

        private async Task<HospitalService> GetConsultationServiceAsync()
        {
            var service = await _context.HospitalServices
                .FirstOrDefaultAsync(s => s.Name == "Consultation" && s.IsActive);

            if (service != null) return service;

            service = new HospitalService
            {
                Name = "Consultation",
                Category = "Appointment",
                Price = 100.00m,
                IsActive = true
            };

            _context.HospitalServices.Add(service);
            await _context.SaveChangesAsync();
            return service;
        }

        private async Task<List<BillItem>> BuildBillItemsAsync(List<AppointmentBillItemModel> serviceItems)
        {
            var items = new List<BillItem>();

            foreach (var item in serviceItems)
            {
                if (item.Quantity <= 0) item.Quantity = 1;

                HospitalService service = null;
                if (item.HospitalServiceId.HasValue)
                {
                    service = await _context.HospitalServices
                        .FirstOrDefaultAsync(s => s.Id == item.HospitalServiceId.Value && s.IsActive);

                    if (service == null)
                        throw new InvalidOperationException($"Hospital service #{item.HospitalServiceId.Value} was not found.");
                }

                var description = service?.Name ?? item.Description;
                if (string.IsNullOrWhiteSpace(description))
                    throw new InvalidOperationException("Each invoice item needs a service or description.");

                var unitPrice = item.UnitPrice ?? service?.Price ?? 0;
                var lineTotal = unitPrice * item.Quantity;

                items.Add(new BillItem
                {
                    HospitalServiceId = service?.Id,
                    Description = description,
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal
                });
            }

            return items;
        }

        private static bool IsValidPaymentStatus(string status)
        {
            return status == PendingStatus || status == PaidStatus || status == CancelledStatus;
        }

        private async Task<int?> GetCurrentPatientIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            var patient = await _context.Patients
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            return patient?.Id;
        }
    }

    public class ManualBillModel
    {
        public int PatientId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public string Notes { get; set; }
    }

    public class AppointmentBillModel
    {
        public List<AppointmentBillItemModel> Items { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public string Notes { get; set; }
    }

    public class AppointmentBillItemModel
    {
        public int? HospitalServiceId { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
    }

    public class BillStatusUpdateModel
    {
        public string PaymentStatus { get; set; }
    }

    public class HospitalServiceModel
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
    }
}
