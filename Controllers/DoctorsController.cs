using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Hubs;
using SHMS.Backend.Models;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class DoctorsController : ControllerBase
    {
        private readonly SHMSDbContext _context;
        private readonly IHubContext<HospitalHub> _hubContext;

        public DoctorsController(SHMSDbContext context, IHubContext<HospitalHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDoctors()
        {
            var doctors = await _context.Doctors
                .Include(d => d.User)
                .ToListAsync();
            return Ok(doctors);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDoctorById(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doctor == null) return NotFound(new { Message = "Doctor not found" });
            return Ok(doctor);
        }

        [HttpPut("{id}/availability")]
        public async Task<IActionResult> UpdateAvailability(int id, [FromBody] DoctorAvailabilityModel model)
        {
            var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id);
            if (doctor == null) return NotFound(new { Message = "Doctor not found" });

            doctor.IsAvailable = model.IsAvailable;
            await _context.SaveChangesAsync();

            // Broadcast real-time availability change
            await _hubContext.Clients.All.SendAsync("DoctorStatusChanged", doctor.Id, doctor.IsAvailable);

            return Ok(doctor);
        }

        [HttpPut("{id}/roster")]
        public async Task<IActionResult> UpdateRoster(int id, [FromBody] DoctorRosterModel model)
        {
            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null) return NotFound(new { Message = "Doctor not found" });

            doctor.RosterSchedule = model.RosterSchedule ?? doctor.RosterSchedule;
            doctor.Specialization = model.Specialization ?? doctor.Specialization;
            await _context.SaveChangesAsync();

            return Ok(doctor);
        }
    }

    public class DoctorAvailabilityModel
    {
        public bool IsAvailable { get; set; }
    }

    public class DoctorRosterModel
    {
        public string RosterSchedule { get; set; }
        public string Specialization { get; set; }
    }
}
