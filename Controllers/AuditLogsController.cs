using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AuditLogsController : ControllerBase
    {
        private readonly SHMSDbContext _context;

        public AuditLogsController(SHMSDbContext context)
        {
            _context = context;
        }

        // GET /api/auditlogs
        // Optional filters: userId, role, action, patientId, from, to, page, pageSize
        // This is the only endpoint — no POST, PUT, or DELETE exist by design (immutability).
        [HttpGet]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] string userId   = null,
            [FromQuery] string role     = null,
            [FromQuery] string action   = null,
            [FromQuery] int?   patientId = null,
            [FromQuery] DateTime? from  = null,
            [FromQuery] DateTime? to    = null,
            [FromQuery] int page        = 1,
            [FromQuery] int pageSize    = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            page     = Math.Max(1, page);

            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(l => l.UserId == userId || l.UserName.Contains(userId));

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(l => l.UserRole == role);

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(l => l.Action == action);

            if (patientId.HasValue)
                query = query.Where(l => l.PatientId == patientId.Value);

            if (from.HasValue)
                query = query.Where(l => l.Timestamp >= from.Value.ToUniversalTime());

            if (to.HasValue)
                query = query.Where(l => l.Timestamp <= to.Value.ToUniversalTime());

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                total,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)total / pageSize),
                logs
            });
        }
    }
}
