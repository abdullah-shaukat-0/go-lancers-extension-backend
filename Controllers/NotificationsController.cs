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
    public class NotificationsController : ControllerBase
    {
        private readonly SHMSDbContext _context;

        public NotificationsController(SHMSDbContext context)
        {
            _context = context;
        }

        // GET /api/notifications/patient/{patientId} — Patient's full inbox
        [HttpGet("patient/{patientId}")]
        public async Task<IActionResult> GetForPatient(int patientId)
        {
            var notifications = await _context.PatientNotifications
                .Where(n => n.PatientId == patientId)
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();

            return Ok(notifications.Select(n => MapToDto(n)));
        }

        // GET /api/notifications/unread/{patientId} — Unread count
        [HttpGet("unread/{patientId}")]
        public async Task<IActionResult> GetUnreadCount(int patientId)
        {
            var count = await _context.PatientNotifications
                .Where(n => n.PatientId == patientId && !n.IsRead)
                .CountAsync();

            return Ok(new { unreadCount = count });
        }

        // GET /api/notifications/sent — What current doctor/nurse has sent (filtered by senderId in query)
        [HttpGet("sent")]
        [Authorize(Roles = "Doctor,Nurse,Admin")]
        public async Task<IActionResult> GetSent([FromQuery] string senderId)
        {
            var query = _context.PatientNotifications
                .Include(n => n.Patient).ThenInclude(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(senderId))
                query = query.Where(n => n.SenderId == senderId);

            var notifications = await query
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();

            return Ok(notifications.Select(n => new
            {
                id = n.Id,
                patientId = n.PatientId,
                patientName = n.Patient?.User?.FullName ?? "Unknown",
                subject = n.Subject,
                message = n.Message,
                notificationType = n.NotificationType,
                isRead = n.IsRead,
                sentAt = n.SentAt,
                scheduledFor = n.ScheduledFor,
                isEmailSent = n.IsEmailSent,
                senderName = n.SenderName,
                senderRole = n.SenderRole
            }));
        }

        // GET /api/notifications/staff-summary — Bell badge + quick summary for doctors/nurses
        [HttpGet("staff-summary")]
        [Authorize(Roles = "Doctor,Nurse,Admin")]
        public async Task<IActionResult> GetStaffSummary([FromQuery] string senderId)
        {
            if (string.IsNullOrEmpty(senderId))
                return BadRequest(new { Message = "senderId is required." });

            // Count = notifications this staff member sent that the patient has NOT yet read
            var unreadByPatientCount = await _context.PatientNotifications
                .Where(n => n.SenderId == senderId && !n.IsRead)
                .CountAsync();

            // Recent items = last 5 sent (no date cutoff)
            var recentItems = await _context.PatientNotifications
                .Include(n => n.Patient).ThenInclude(p => p.User)
                .Where(n => n.SenderId == senderId)
                .OrderByDescending(n => n.SentAt)
                .Take(5)
                .ToListAsync();

            return Ok(new
            {
                recentCount = unreadByPatientCount,
                recent = recentItems.Select(n => new
                {
                    id = n.Id,
                    patientName = n.Patient?.User?.FullName ?? "Unknown",
                    subject = n.Subject,
                    notificationType = n.NotificationType,
                    sentAt = n.SentAt,
                    scheduledFor = n.ScheduledFor,
                    isEmailSent = n.IsEmailSent
                })
            });
        }

        // POST /api/notifications/send — Doctor or Nurse sends a notification now
        [HttpPost("send")]
        [Authorize(Roles = "Doctor,Nurse,Admin")]
        public async Task<IActionResult> Send([FromBody] SendNotificationDto dto)
        {
            var patient = await _context.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == dto.PatientId);
            if (patient == null) return BadRequest(new { Message = "Patient not found." });

            var notification = new PatientNotification
            {
                PatientId = dto.PatientId,
                SenderId = dto.SenderId,
                SenderName = dto.SenderName,
                SenderRole = dto.SenderRole,
                Subject = dto.Subject,
                Message = dto.Message,
                NotificationType = dto.NotificationType ?? "General",
                IsRead = false,
                SentAt = DateTime.UtcNow,
                ScheduledFor = null,
                IsEmailSent = true  // simulated email dispatch
            };

            _context.PatientNotifications.Add(notification);
            await _context.SaveChangesAsync();

            // Simulate direct email delivery to the patient for treatments or prescriptions
            Console.WriteLine($"[EMAIL SERVICE] Sending secure clinical email to patient {patient.User?.Email} regarding {dto.Subject}: \"{dto.Message}\"");

            // Log event to security audit logs (PHIPA requirement)
            var auditService = HttpContext.RequestServices.GetService(typeof(Services.IAuditService)) as Services.IAuditService;
            if (auditService != null)
            {
                await auditService.LogAsync("PHI_WRITE", "Notifications", notification.Id.ToString(), $"Clinical email notification sent to patient {patient.User?.FullName ?? patient.Id.ToString()} regarding {dto.Subject}", "Success");
            }

            return Ok(new { Message = "Notification sent successfully.", id = notification.Id });
        }

        // POST /api/notifications/schedule — Schedule a future notification
        [HttpPost("schedule")]
        [Authorize(Roles = "Doctor,Nurse,Admin")]
        public async Task<IActionResult> Schedule([FromBody] ScheduleNotificationDto dto)
        {
            var patient = await _context.Patients.FindAsync(dto.PatientId);
            if (patient == null) return BadRequest(new { Message = "Patient not found." });

            var notification = new PatientNotification
            {
                PatientId = dto.PatientId,
                SenderId = dto.SenderId,
                SenderName = dto.SenderName,
                SenderRole = dto.SenderRole,
                Subject = dto.Subject,
                Message = dto.Message,
                NotificationType = dto.NotificationType ?? "General",
                IsRead = false,
                SentAt = DateTime.UtcNow,
                ScheduledFor = dto.ScheduledFor,
                IsEmailSent = false  // will be dispatched when scheduled time arrives
            };

            _context.PatientNotifications.Add(notification);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Notification scheduled successfully.", id = notification.Id, scheduledFor = dto.ScheduledFor });
        }

        // PUT /api/notifications/{id}/read — Mark a notification as read
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var notification = await _context.PatientNotifications.FindAsync(id);
            if (notification == null) return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Marked as read." });
        }

        // DELETE /api/notifications/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor,Nurse,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var notification = await _context.PatientNotifications.FindAsync(id);
            if (notification == null) return NotFound();

            _context.PatientNotifications.Remove(notification);
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Deleted." });
        }

        // GET /api/notifications/patients — list all patients for sender dropdown
        [HttpGet("patients")]
        [Authorize(Roles = "Doctor,Nurse,Admin")]
        public async Task<IActionResult> GetPatients()
        {
            var patients = await _context.Patients
                .Include(p => p.User)
                .ToListAsync();

            return Ok(patients.Select(p => new {
                id = p.Id,
                fullName = p.User?.FullName ?? "Patient",
                email = p.User?.Email ?? ""
            }));
        }

        private static object MapToDto(PatientNotification n)
        {
            return new
            {
                id = n.Id,
                patientId = n.PatientId,
                senderId = n.SenderId,
                senderName = n.SenderName,
                senderRole = n.SenderRole,
                subject = n.Subject,
                message = n.Message,
                notificationType = n.NotificationType,
                isRead = n.IsRead,
                sentAt = n.SentAt,
                scheduledFor = n.ScheduledFor,
                isEmailSent = n.IsEmailSent
            };
        }
    }

    public class SendNotificationDto
    {
        public int PatientId { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderRole { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string NotificationType { get; set; }
    }

    public class ScheduleNotificationDto
    {
        public int PatientId { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderRole { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string NotificationType { get; set; }
        public DateTime ScheduledFor { get; set; }
    }
}
