using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Doctor,Nurse")]
    public class PatientsController : ControllerBase
    {
        private readonly SHMSDbContext _context;
        private readonly Services.IAuditService _auditService;

        public PatientsController(SHMSDbContext context, Services.IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPatients()
        {
            var patients = await _context.Patients
                .Include(p => p.User)
                .ToListAsync();
            await _auditService.LogAsync("PHI_READ", "Patients", "ALL", "Accessed list of all patients", "Success");
            return Ok(patients);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPatientById(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null)
            {
                await _auditService.LogAsync("PHI_READ", "Patients", id.ToString(), $"Attempted to access patient details but patient was not found", "Failure");
                return NotFound(new { Message = "Patient not found" });
            }

            await _auditService.LogAsync("PHI_READ", "Patients", id.ToString(), $"Accessed details of patient {patient.User?.FullName ?? id.ToString()}", "Success");
            return Ok(patient);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Doctor")] // PHIPA: Nurses have read-only access to patient medical records
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

            await _auditService.LogAsync("PHI_WRITE", "Patients", id.ToString(), $"Updated medical history/profile details for patient {patient.User?.FullName ?? id.ToString()}", "Success");

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
