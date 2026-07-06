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

        public CareInstructionsController(SHMSDbContext context)
        {
            _context = context;
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
                return BadRequest(new { Message = "Invalid patient, doctor, or nurse ID." });

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

            return Ok(MapToDto(created));
        }

        // PUT /api/careinstructions/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CareInstructionUpdateDto dto)
        {
            var instruction = await _context.CareInstructions.FindAsync(id);
            if (instruction == null) return NotFound();

            if (!string.IsNullOrEmpty(dto.Status)) instruction.Status = dto.Status;
            if (dto.NurseNotes != null) instruction.NurseNotes = dto.NurseNotes;
            if (!string.IsNullOrEmpty(dto.Priority)) instruction.Priority = dto.Priority;
            if (!string.IsNullOrEmpty(dto.Instructions)) instruction.Instructions = dto.Instructions;
            instruction.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { Message = "Updated successfully." });
        }

        // DELETE /api/careinstructions/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Doctor,Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var instruction = await _context.CareInstructions.FindAsync(id);
            if (instruction == null) return NotFound();

            _context.CareInstructions.Remove(instruction);
            await _context.SaveChangesAsync();
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
