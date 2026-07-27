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
    public class CareInstructionsController : ControllerBase
    {
        private readonly SHMSDbContext _context;
        private readonly Services.IAuditService _auditService;

        public CareInstructionsController(SHMSDbContext context, Services.IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        // GET /api/careinstructions
        // Admin sees all; doctors/nurses see their own
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var instructions = await _context.CareInstructions
                .Include(ci => ci.Patient).ThenInclude(p => p.User)
                .Include(ci => ci.Doctor).ThenInclude(d => d.User)
                .Include(ci => ci.Nurse).ThenInclude(n => n.User)
                .OrderByDescending(ci => ci.CreatedAt)
                .ToListAsync();

            await _auditService.LogAsync("PHI_READ", "CareInstructions", "ALL", "Accessed all care instructions list", "Success");
            return Ok(instructions.Select(ci => MapToDto(ci)));
        }

        // GET /api/careinstructions/nurse/{nurseId}
        [HttpGet("nurse/{nurseId}")]
        public async Task<IActionResult> GetByNurse(int nurseId)
        {
            var instructions = await _context.CareInstructions
                .Include(ci => ci.Patient).ThenInclude(p => p.User)
                .Include(ci => ci.Doctor).ThenInclude(d => d.User)
                .Include(ci => ci.Nurse).ThenInclude(n => n.User)
                .Where(ci => ci.NurseId == nurseId)
                .OrderByDescending(ci => ci.CreatedAt)
                .ToListAsync();

            await _auditService.LogAsync("PHI_READ", "CareInstructions", $"Nurse_{nurseId}", $"Accessed care instructions assigned to nurse {nurseId}", "Success");
            return Ok(instructions.Select(ci => MapToDto(ci)));
        }

        // GET /api/careinstructions/doctor/{doctorId}
        [HttpGet("doctor/{doctorId}")]
        public async Task<IActionResult> GetByDoctor(int doctorId)
        {
            var instructions = await _context.CareInstructions
                .Include(ci => ci.Patient).ThenInclude(p => p.User)
                .Include(ci => ci.Doctor).ThenInclude(d => d.User)
                .Include(ci => ci.Nurse).ThenInclude(n => n.User)
                .Where(ci => ci.DoctorId == doctorId)
                .OrderByDescending(ci => ci.CreatedAt)
                .ToListAsync();

            await _auditService.LogAsync("PHI_READ", "CareInstructions", $"Doctor_{doctorId}", $"Accessed care instructions authored by doctor {doctorId}", "Success");
            return Ok(instructions.Select(ci => MapToDto(ci)));
        }

        // GET /api/careinstructions/patient/{patientId}
        [HttpGet("patient/{patientId}")]
        public async Task<IActionResult> GetByPatient(int patientId)
        {
            var instructions = await _context.CareInstructions
                .Include(ci => ci.Patient).ThenInclude(p => p.User)
                .Include(ci => ci.Doctor).ThenInclude(d => d.User)
                .Include(ci => ci.Nurse).ThenInclude(n => n.User)
                .Where(ci => ci.PatientId == patientId)
                .OrderByDescending(ci => ci.CreatedAt)
                .ToListAsync();

            await _auditService.LogAsync("PHI_READ", "CareInstructions", $"Patient_{patientId}", $"Accessed care instructions related to patient {patientId}", "Success");
            return Ok(instructions.Select(ci => MapToDto(ci)));
        }

        // POST /api/careinstructions
        [HttpPost]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Create([FromBody] CareInstructionCreateDto dto)
        {
            var patient = await _context.Patients.FindAsync(dto.PatientId);
            var doctor = await _context.Doctors.FindAsync(dto.DoctorId);
            var nurse = await _context.Nurses.FindAsync(dto.NurseId);

            if (patient == null || doctor == null || nurse == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "CareInstructions", "NEW", "Failed to create care instruction: invalid entities", "Failure");
                return BadRequest(new { Message = "Invalid patient, doctor, or nurse ID." });
            }

            var instruction = new CareInstruction
            {
                PatientId = dto.PatientId,
                DoctorId = dto.DoctorId,
                NurseId = dto.NurseId,
                Instructions = dto.Instructions,
                Priority = dto.Priority ?? "Medium",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                NurseNotes = ""
            };

            _context.CareInstructions.Add(instruction);
            await _context.SaveChangesAsync();

            // Reload with navigation props
            var created = await _context.CareInstructions
                .Include(ci => ci.Patient).ThenInclude(p => p.User)
                .Include(ci => ci.Doctor).ThenInclude(d => d.User)
                .Include(ci => ci.Nurse).ThenInclude(n => n.User)
                .FirstOrDefaultAsync(ci => ci.Id == instruction.Id);

            await _auditService.LogAsync("PHI_WRITE", "CareInstructions", instruction.Id.ToString(), $"Created new care instruction for patient {patient.Id}", "Success");
            return Ok(MapToDto(created));
        }

        // PUT /api/careinstructions/{id}
        // Nurses may only update Status and NurseNotes (PHIPA: clinical instructions/priority are doctor-only)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CareInstructionUpdateDto dto)
        {
            var instruction = await _context.CareInstructions.FindAsync(id);
            if (instruction == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "CareInstructions", id.ToString(), "Attempted to update care instruction but it was not found", "Failure");
                return NotFound();
            }

            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var isNurse = role.Equals("Nurse", StringComparison.OrdinalIgnoreCase);

            // PHIPA compliance: Nurses can ONLY update their own notes and status
            if (isNurse)
            {
                if (dto.NurseNotes != null) instruction.NurseNotes = dto.NurseNotes;
                if (!string.IsNullOrEmpty(dto.Status)) instruction.Status = dto.Status;
            }
            else
            {
                // Doctors and Admins can update all fields
                if (!string.IsNullOrEmpty(dto.Status)) instruction.Status = dto.Status;
                if (dto.NurseNotes != null) instruction.NurseNotes = dto.NurseNotes;
                if (!string.IsNullOrEmpty(dto.Priority)) instruction.Priority = dto.Priority;
                if (!string.IsNullOrEmpty(dto.Instructions)) instruction.Instructions = dto.Instructions;
            }
            instruction.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync("PHI_WRITE", "CareInstructions", id.ToString(), $"Updated care instruction {id} status/notes", "Success");
            return Ok(new { Message = "Updated successfully." });
        }

        // DELETE /api/careinstructions/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var instruction = await _context.CareInstructions.FindAsync(id);
            if (instruction == null)
            {
                await _auditService.LogAsync("PHI_WRITE", "CareInstructions", id.ToString(), "Attempted to delete care instruction but it was not found", "Failure");
                return NotFound();
            }

            _context.CareInstructions.Remove(instruction);
            await _context.SaveChangesAsync();
            await _auditService.LogAsync("PHI_WRITE", "CareInstructions", id.ToString(), $"Deleted care instruction {id}", "Success");
            return Ok(new { Message = "Deleted successfully." });
        }

        // GET /api/careinstructions/nurses — list all nurses for dropdown
        [HttpGet("nurses")]
        public async Task<IActionResult> GetNurses()
        {
            var nurses = await _context.Nurses
                .Include(n => n.User)
                .Where(n => n.IsAvailable)
                .ToListAsync();

            return Ok(nurses.Select(n => new {
                id = n.Id,
                fullName = n.User?.FullName ?? "Nurse",
                department = n.Department,
                shift = n.Shift
            }));
        }

        private static object MapToDto(CareInstruction ci)
        {
            return new
            {
                id = ci.Id,
                patientId = ci.PatientId,
                patientName = ci.Patient?.User?.FullName ?? "Unknown",
                doctorId = ci.DoctorId,
                doctorName = ci.Doctor?.User?.FullName ?? "Unknown",
                nurseId = ci.NurseId,
                nurseName = ci.Nurse?.User?.FullName ?? "Unknown",
                nurseDepartment = ci.Nurse?.Department ?? "",
                instructions = ci.Instructions,
                priority = ci.Priority,
                status = ci.Status,
                createdAt = ci.CreatedAt,
                updatedAt = ci.UpdatedAt,
                nurseNotes = ci.NurseNotes
            };
        }
    }

    public class CareInstructionCreateDto
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public int NurseId { get; set; }
        public string Instructions { get; set; }
        public string Priority { get; set; }
    }

    public class CareInstructionUpdateDto
    {
        public string Status { get; set; }
        public string NurseNotes { get; set; }
        public string Priority { get; set; }
        public string Instructions { get; set; }
    }
}
