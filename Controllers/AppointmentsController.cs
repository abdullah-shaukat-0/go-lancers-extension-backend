using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    public class AppointmentsController : ControllerBase
    {
        private readonly SHMSDbContext _context;

        public Microsoft.AspNetCore.SignalR.IHubContext<SHMS.Backend.Hubs.HospitalHub> _hubContext { get; }

        public AppointmentsController(SHMSDbContext context, Microsoft.AspNetCore.SignalR.IHubContext<SHMS.Backend.Hubs.HospitalHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
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
            return Ok(appointments);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointmentById(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound(new { Message = "Appointment not found" });
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

            // Auto-generate a billing entry for the appointment (e.g. flat consultation fee of $100)
            var bill = new Bill
            {
                PatientId = model.PatientId,
                AppointmentId = appointment.Id,
                Amount = 100.00m,
                PaymentStatus = "Pending",
                DateGenerated = DateTime.UtcNow
            };
            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            // Broadcast real-time booking alert to system dashboards
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", "System", $"New appointment booked for Patient #{model.PatientId}");

            return CreatedAtAction(nameof(GetAppointmentById), new { id = appointment.Id }, appointment);
        }

        [HttpPut("{id}/reschedule")]
        public async Task<IActionResult> RescheduleAppointment(int id, [FromBody] AppointmentRescheduleModel model)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound(new { Message = "Appointment not found" });

            // Check if there is an existing confirmed appointment for the same doctor on the same date/time
            var conflict = await _context.Appointments
                .AnyAsync(a => a.Id != id
                    && a.DoctorId == appointment.DoctorId 
                    && a.AppointmentDate == model.AppointmentDate 
                    && a.Status != "Cancelled" 
                    && _context.Bills.Any(b => b.AppointmentId == a.Id && b.PaymentStatus == "Paid"));

            if (conflict)
            {
                return BadRequest(new { Message = "This slot is already booked and confirmed for this doctor. Please select another date or time." });
            }

            appointment.AppointmentDate = model.AppointmentDate;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("AppointmentUpdated", id, "Rescheduled");

            return Ok(appointment);
        }

        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteAppointment(int id, [FromBody] AppointmentCompleteModel model)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound(new { Message = "Appointment not found" });

            appointment.Status = "Completed";
            appointment.Diagnosis = model.Diagnosis ?? appointment.Diagnosis;
            appointment.Prescription = model.Prescription ?? appointment.Prescription;

            await _context.SaveChangesAsync();

            return Ok(appointment);
        }

        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null) return NotFound(new { Message = "Appointment not found" });

            appointment.Status = "Cancelled";
            await _context.SaveChangesAsync();

            // Find bill and mark it cancelled or remove it
            var bill = await _context.Bills.FirstOrDefaultAsync(b => b.AppointmentId == id);
            if (bill != null && bill.PaymentStatus == "Pending")
            {
                _context.Bills.Remove(bill);
                await _context.SaveChangesAsync();
            }

            await _hubContext.Clients.All.SendAsync("AppointmentUpdated", id, "Cancelled");

            return Ok(appointment);
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
