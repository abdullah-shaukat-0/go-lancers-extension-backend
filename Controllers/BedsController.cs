using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Hubs;
using SHMS.Backend.Models;
using SHMS.Backend.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,Doctor,Nurse")]
    public class BedsController : ControllerBase
    {
        private readonly SHMSDbContext _context;
        private readonly IHubContext<HospitalHub> _hubContext;
        private readonly IAuditService _auditService;

        public BedsController(SHMSDbContext context, IHubContext<HospitalHub> hubContext, IAuditService auditService)
        {
            _context = context;
            _hubContext = hubContext;
            _auditService = auditService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBeds()
        {
            var beds = await _context.Beds
                .OrderBy(b => b.RoomNumber)
                .Include(b => b.Patient).ThenInclude(p => p.User)
                .ToListAsync();

            await _auditService.LogAsync(new AuditLogEntry
            {
                Action       = "VIEW_BED_LIST",
                ResourceType = "Bed",
                ResourceId   = "all",
                Details      = $"Retrieved bed list ({beds.Count} beds)"
            });

            return Ok(beds);
        }

        [HttpPut("{id}/occupy")]
        public async Task<IActionResult> OccupyBed(int id, [FromBody] BedOccupyModel model)
        {
            var bed = await _context.Beds.FindAsync(id);
            if (bed == null) return NotFound(new { Message = "Bed not found" });

            var patient = await _context.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == model.PatientId);
            if (patient == null) return BadRequest(new { Message = "Invalid Patient ID" });

            bed.IsOccupied = true;
            bed.PatientId = model.PatientId;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(new AuditLogEntry
            {
                PatientId    = model.PatientId,
                Action       = "ALLOCATE_BED",
                ResourceType = "Bed",
                ResourceId   = id.ToString(),
                Details      = $"Bed #{id} allocated to patient #{model.PatientId}"
            });

            // Broadcast real-time bed occupancy update
            await _hubContext.Clients.All.SendAsync("BedStatusChanged", bed.Id, true, patient.User.FullName);

            return Ok(bed);
        }

        [HttpPut("{id}/release")]
        public async Task<IActionResult> ReleaseBed(int id)
        {
            var bed = await _context.Beds.FindAsync(id);
            if (bed == null) return NotFound(new { Message = "Bed not found" });

            bed.IsOccupied = false;
            bed.PatientId = null;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(new AuditLogEntry
            {
                Action       = "RELEASE_BED",
                ResourceType = "Bed",
                ResourceId   = id.ToString(),
                Details      = $"Bed #{id} released (patient discharged)"
            });

            // Broadcast real-time bed release update
            await _hubContext.Clients.All.SendAsync("BedStatusChanged", bed.Id, false, "");

            return Ok(bed);
        }
    }

    public class BedOccupyModel
    {
        public int PatientId { get; set; }
    }
}
