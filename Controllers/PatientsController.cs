using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
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

        public PatientsController(SHMSDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Doctor,Nurse")]
        public async Task<IActionResult> GetAllPatients()
        {
            var patients = await _context.Patients
                .Include(p => p.User)
                .ToListAsync();
            return Ok(patients);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPatientById(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null) return NotFound(new { Message = "Patient not found" });
            if (User.IsInRole("Patient") && patient.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                return Forbid();

            return Ok(patient);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> UpdatePatient(int id, [FromBody] PatientUpdateModel model)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound(new { Message = "Patient not found" });

            patient.MedicalHistory = model.MedicalHistory ?? patient.MedicalHistory;
            patient.BloodGroup = model.BloodGroup ?? patient.BloodGroup;
            patient.Gender = model.Gender ?? patient.Gender;
            if (model.DateOfBirth.HasValue) patient.DateOfBirth = model.DateOfBirth.Value;

            _context.Entry(patient).State = EntityState.Modified;
            await _context.SaveChangesAsync();

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
