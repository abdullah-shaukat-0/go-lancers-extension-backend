using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using SHMS.Backend.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PatientsController : ControllerBase
    {
        private readonly SHMSDbContext _context;
        private readonly IAuditService _auditService;

        public PatientsController(SHMSDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Doctor,Nurse")]
        public async Task<IActionResult> GetAllPatients()
        {
            var patients = await _context.Patients
                .Include(p => p.User)
                .ToListAsync();

            await _auditService.LogAsync(new AuditLogEntry
            {
                Action       = "VIEW_PATIENT_LIST",
                ResourceType = "Patient",
                ResourceId   = "all",
                Details      = $"Retrieved list of {patients.Count} patients"
            });

            return Ok(patients);
        }

        [HttpGet("{id:int}")]
        [Authorize]
        public async Task<IActionResult> GetPatientById(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentPatientId = await _context.Patients
                .Where(p => p.UserId == userId)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync();

            if (currentPatientId.HasValue)
            {
                id = currentPatientId.Value;
            }
            else if (!User.IsInRole("Admin") && !User.IsInRole("Doctor") && !User.IsInRole("Nurse"))
            {
                return Forbid();
            }

            var patient = await _context.Patients
                .Include(p => p.User)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.UserId,
                    p.MedicalHistory,
                    p.BloodGroup,
                    p.Gender,
                    p.DateOfBirth,
                    User = new
                    {
                        p.User.FullName,
                        p.User.Email,
                        p.User.UserName
                    }
                })
                .FirstOrDefaultAsync();

            if (patient == null) return NotFound(new { Message = "Patient not found" });

            await _auditService.LogAsync(new AuditLogEntry
            {
                PatientId    = id,
                Action       = "VIEW_PATIENT_PROFILE",
                ResourceType = "Patient",
                ResourceId   = id.ToString(),
                Details      = $"Viewed profile for patient #{id}"
            });

            return Ok(patient);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentPatient()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
                return Forbid();

            var patient = await _context.Patients
                .Where(p => p.UserId == userId)
                .Select(p => new
                {
                    p.Id,
                    p.UserId,
                    p.MedicalHistory,
                    p.BloodGroup,
                    p.Gender,
                    p.DateOfBirth,
                    User = new
                    {
                        p.User.FullName,
                        p.User.Email,
                        p.User.UserName
                    }
                })
                .FirstOrDefaultAsync();

            if (patient == null)
                return NotFound(new { Message = "Patient profile not found for current user" });

            await _auditService.LogAsync(new AuditLogEntry
            {
                PatientId    = patient.Id,
                Action       = "VIEW_PATIENT_PROFILE",
                ResourceType = "Patient",
                ResourceId   = patient.Id.ToString(),
                Details      = "Viewed own patient profile"
            });

            return Ok(patient);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> UpdatePatient(int id, [FromBody] PatientUpdateModel model)
        {
            var patient = await _context.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
            if (patient == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "Patients", id.ToString(), "Attempted to update patient details but patient was not found", "Failure");
                return NotFound(new { Message = "Patient not found" });
            }

            patient.MedicalHistory = model.MedicalHistory ?? patient.MedicalHistory;
            patient.BloodGroup = model.BloodGroup ?? patient.BloodGroup;
            patient.Gender = model.Gender ?? patient.Gender;
            if (model.DateOfBirth.HasValue) patient.DateOfBirth = model.DateOfBirth.Value;

            _context.Entry(patient).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(new AuditLogEntry
            {
                PatientId    = id,
                Action       = "UPDATE_PATIENT_PROFILE",
                ResourceType = "Patient",
                ResourceId   = id.ToString(),
                Details      = $"Updated profile fields for patient #{id}"
            });

            return Ok(patient);
        }
    }

    public class PatientUpdateModel
    {
        public string MedicalHistory { get; set; }
        public string BloodGroup { get; set; }
        public string Gender { get; set; }
        public System.DateTime? DateOfBirth { get; set; }
    }
}
