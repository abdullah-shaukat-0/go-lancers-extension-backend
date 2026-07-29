using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AppointmentsController : ControllerBase
    {
        private readonly SHMSDbContext _context;
        private readonly Services.IAuditService _auditService;

        public Microsoft.AspNetCore.SignalR.IHubContext<SHMS.Backend.Hubs.HospitalHub> _hubContext { get; }

        public AppointmentsController(
            SHMSDbContext context, 
            Microsoft.AspNetCore.SignalR.IHubContext<SHMS.Backend.Hubs.HospitalHub> hubContext,
            Services.IAuditService auditService)
        {
            _context = context;
            _hubContext = hubContext;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAppointments(int? patientId, int? doctorId)
        {
            var query = _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .AsQueryable();

            if (patientId.HasValue)
                query = query.Where(a => a.PatientId == patientId.Value);

            if (doctorId.HasValue)
                query = query.Where(a => a.DoctorId == doctorId.Value);

            var appointments = await query.OrderByDescending(a => a.AppointmentDate).ToListAsync();
            await _auditService.LogAsync("PHI_READ", "Appointments", patientId?.ToString() ?? "ALL", "Accessed appointments list", "Success");
            return Ok(appointments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointmentById(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                await _auditService.LogAsync("PHI_READ", "Appointments", id.ToString(), "Attempted to view appointment details but it was not found", "Failure");
                return NotFound(new { Message = "Appointment not found" });
            }

            await _auditService.LogAsync("PHI_READ", "Appointments", id.ToString(), $"Viewed appointment details for patient {appointment.PatientId}", "Success");
            return Ok(appointment);
        }

        [HttpPost]
        public async Task<IActionResult> BookAppointment([FromBody] AppointmentCreateModel model)
        {
            // Check if there is an existing confirmed appointment for the same doctor on the same date/time
            var conflict = await _context.Appointments
                .AnyAsync(a => a.DoctorId == model.DoctorId 
                    && a.AppointmentDate == model.AppointmentDate 
                    && a.Status != "Cancelled" 
                    && _context.Bills.Any(b => b.AppointmentId == a.Id && b.PaymentStatus == "Paid"));

            if (conflict)
            {
                await _auditService.LogAsync("PHI_WRITE", "Appointments", "NEW", $"Failed to book appointment for Doctor {model.DoctorId} at {model.AppointmentDate}: Time Slot Conflict", "Failure");
                return BadRequest(new { Message = "This slot is already booked and confirmed for this doctor. Please select another date or time." });
            }

            var appointment = new Appointment
            {
                PatientId = model.PatientId,
                DoctorId = model.DoctorId,
                AppointmentDate = model.AppointmentDate,
                Status = "Scheduled",
                Symptoms = model.Symptoms,
                Diagnosis = "",
                Prescription = ""
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var consultationService = await GetOrCreateConsultationServiceAsync();

            // Auto-generate a consultation invoice for the appointment.
            var bill = new Bill
            {
                PatientId = model.PatientId,
                AppointmentId = appointment.Id,
                InvoiceNumber = await GenerateInvoiceNumberAsync(),
                Subtotal = consultationService.Price,
                DiscountAmount = 0,
                TaxAmount = 0,
                Amount = consultationService.Price,
                PaymentStatus = "Pending",
                Notes = "Auto-generated from appointment booking.",
                DateGenerated = DateTime.UtcNow,
                Items = new List<BillItem>
                {
                    new BillItem
                    {
                        HospitalServiceId = consultationService.Id,
                        Description = consultationService.Name,
                        Quantity = 1,
                        UnitPrice = consultationService.Price,
                        LineTotal = consultationService.Price
                    }
                }
            };
            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            // Broadcast real-time booking alert to system dashboards
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"New appointment booked for Patient #{model.PatientId}");

            await _auditService.LogAsync("PHI_WRITE", "Appointments", appointment.Id.ToString(), $"Booked new appointment with Doctor {model.DoctorId} on {model.AppointmentDate}", "Success");
            return CreatedAtAction(nameof(GetAppointmentById), new { id = appointment.Id }, appointment);
        }

        [HttpPost("{id}/reschedule")]
        [HttpPut("{id}/reschedule")]
        public async Task<IActionResult> RescheduleAppointment(int id, [FromBody] AppointmentRescheduleModel model)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "Appointments", id.ToString(), "Attempted to reschedule appointment but it was not found", "Failure");
                return NotFound(new { Message = "Appointment not found" });
            }

            // Check if there is an existing confirmed appointment for the same doctor on the same date/time
            var conflict = await _context.Appointments
                .AnyAsync(a => a.Id != id
                    && a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate == model.AppointmentDate 
                    && a.Status != "Cancelled" 
                    && _context.Bills.Any(b => b.AppointmentId == a.Id && b.PaymentStatus == "Paid"));

            if (conflict)
            {
                await _auditService.LogAsync("PHI_WRITE", "Appointments", id.ToString(), $"Failed to reschedule appointment {id}: Slot Conflict", "Failure");
                return BadRequest(new { Message = "This slot is already booked and confirmed for this doctor. Please select another date or time." });
            }

            appointment.AppointmentDate = model.AppointmentDate;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync("PHI_WRITE", "Appointments", id.ToString(), $"Rescheduled appointment {id} to {model.AppointmentDate}", "Success");
            return Ok(appointment);
        }

        [HttpPost("{id}/complete")]
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteAppointment(int id, [FromBody] AppointmentCompleteModel model)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "Appointments", id.ToString(), "Attempted to complete appointment but it was not found", "Failure");
                return NotFound(new { Message = "Appointment not found" });
            }

            appointment.Status = "Completed";
            appointment.Diagnosis = model.Diagnosis ?? appointment.Diagnosis;
            appointment.Prescription = model.Prescription ?? appointment.Prescription;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync("PHI_WRITE", "Appointments", id.ToString(), $"Completed appointment {id} and updated diagnosis/prescription", "Success");
            return Ok(appointment);
        }

        [HttpPost("{id}/cancel")]
        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "Appointments", id.ToString(), "Attempted to cancel appointment but it was not found", "Failure");
                return NotFound(new { Message = "Appointment not found" });
            }

            appointment.Status = "Cancelled";
            await _context.SaveChangesAsync();

            // Find bill and mark it cancelled or remove it
            var bill = await _context.Bills.FirstOrDefaultAsync(b => b.AppointmentId == id);
            if (bill != null && bill.PaymentStatus == "Pending")
            {
                _context.Bills.Remove(bill);
                await _context.SaveChangesAsync();
            }

            await _auditService.LogAsync("PHI_WRITE", "Appointments", id.ToString(), $"Cancelled appointment {id} and voided pending bill", "Success");
            return Ok(appointment);
        }

        private async Task<string> GenerateInvoiceNumberAsync()
        {
            var nextId = await _context.Bills.CountAsync() + 1;
            return $"INV-{DateTime.UtcNow:yyyyMMdd}-{nextId:D5}";
        }

        private async Task<HospitalService> GetOrCreateConsultationServiceAsync()
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
    }

    public class AppointmentCreateModel
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Symptoms { get; set; }
    }

    public class AppointmentRescheduleModel
    {
        public DateTime AppointmentDate { get; set; }
    }

    public class AppointmentCompleteModel
    {
        public string Diagnosis { get; set; }
        public string Prescription { get; set; }
    }
}
