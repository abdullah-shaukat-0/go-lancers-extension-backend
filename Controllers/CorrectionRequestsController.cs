using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CorrectionRequestsController : ControllerBase
    {
        private const string PendingStatus = "Pending";
        private const string ApprovedStatus = "Approved";
        private const string RejectedStatus = "Rejected";

        private readonly SHMSDbContext _context;

        public CorrectionRequestsController(SHMSDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> CreateCorrectionRequest([FromBody] CorrectionRequestCreateModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.FieldName) || string.IsNullOrWhiteSpace(model.RequestedValue))
                return BadRequest(new { Message = "FieldName and RequestedValue are required." });

            var fieldName = NormalizeFieldName(model.FieldName);
            if (fieldName == null)
                return BadRequest(new { Message = "FieldName must be MedicalHistory, BloodGroup, Gender, or DateOfBirth." });

            var patient = await GetCurrentPatientAsync();
            if (patient == null) return Forbid();

            var duplicatePending = await _context.PatientCorrectionRequests.AnyAsync(r =>
                r.PatientId == patient.Id &&
                r.FieldName == fieldName &&
                r.Status == PendingStatus);

            if (duplicatePending)
                return BadRequest(new { Message = "A pending correction request already exists for this field." });

            var currentValue = GetPatientFieldValue(patient, fieldName);
            var requestedValue = NormalizeRequestedValue(fieldName, model.RequestedValue);
            if (requestedValue == null)
                return BadRequest(new { Message = "RequestedValue is invalid for the selected field." });

            var request = new PatientCorrectionRequest
            {
                PatientId = patient.Id,
                FieldName = fieldName,
                CurrentValue = currentValue,
                RequestedValue = requestedValue,
                Reason = model.Reason,
                Status = PendingStatus,
                SubmittedAt = DateTime.UtcNow
            };

            _context.PatientCorrectionRequests.Add(request);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCorrectionRequestById), new { id = request.Id }, ToDto(request, patient));
        }

        [HttpGet("my")]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> GetMyCorrectionRequests()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return Forbid();

            var requests = await _context.PatientCorrectionRequests
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Where(r => r.PatientId == patient.Id)
                .OrderByDescending(r => r.SubmittedAt)
                .ToListAsync();

            return Ok(requests.Select(ToDto));
        }

        [HttpGet]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> GetCorrectionRequests(string status)
        {
            var query = _context.PatientCorrectionRequests
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.ReviewedByUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            var requests = await query
                .OrderBy(r => r.Status == PendingStatus ? 0 : 1)
                .ThenByDescending(r => r.SubmittedAt)
                .ToListAsync();

            return Ok(requests.Select(ToDto));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCorrectionRequestById(int id)
        {
            var request = await _context.PatientCorrectionRequests
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.ReviewedByUser)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound(new { Message = "Correction request not found." });

            if (User.IsInRole("Patient"))
            {
                var patient = await GetCurrentPatientAsync();
                if (patient == null || request.PatientId != patient.Id) return Forbid();
            }
            else if (!User.IsInRole("Admin") && !User.IsInRole("Doctor"))
            {
                return Forbid();
            }

            return Ok(ToDto(request));
        }

        [HttpPut("{id}/approve")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> ApproveCorrectionRequest(int id, [FromBody] CorrectionRequestReviewModel model)
        {
            var request = await _context.PatientCorrectionRequests
                .Include(r => r.Patient)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound(new { Message = "Correction request not found." });
            if (request.Status != PendingStatus)
                return BadRequest(new { Message = "Only pending correction requests can be approved." });

            ApplyPatientFieldValue(request.Patient, request.FieldName, request.RequestedValue);
            CompleteReview(request, ApprovedStatus, model?.ReviewNote);

            await _context.SaveChangesAsync();
            return Ok(ToDto(request));
        }

        [HttpPut("{id}/reject")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> RejectCorrectionRequest(int id, [FromBody] CorrectionRequestReviewModel model)
        {
            var request = await _context.PatientCorrectionRequests
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null) return NotFound(new { Message = "Correction request not found." });
            if (request.Status != PendingStatus)
                return BadRequest(new { Message = "Only pending correction requests can be rejected." });

            if (model == null || string.IsNullOrWhiteSpace(model.ReviewNote))
                return BadRequest(new { Message = "ReviewNote is required when rejecting a correction request." });

            CompleteReview(request, RejectedStatus, model.ReviewNote);

            await _context.SaveChangesAsync();
            return Ok(ToDto(request));
        }

        private async Task<Patient> GetCurrentPatientAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId)) return null;

            return await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }

        private void CompleteReview(PatientCorrectionRequest request, string status, string reviewNote)
        {
            request.Status = status;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            request.ReviewNote = reviewNote;
        }

        private static string NormalizeFieldName(string fieldName)
        {
            if (fieldName.Equals("MedicalHistory", StringComparison.OrdinalIgnoreCase)) return "MedicalHistory";
            if (fieldName.Equals("BloodGroup", StringComparison.OrdinalIgnoreCase)) return "BloodGroup";
            if (fieldName.Equals("Gender", StringComparison.OrdinalIgnoreCase)) return "Gender";
            if (fieldName.Equals("DateOfBirth", StringComparison.OrdinalIgnoreCase)) return "DateOfBirth";
            return null;
        }

        private static string GetPatientFieldValue(Patient patient, string fieldName)
        {
            if (fieldName == "MedicalHistory") return patient.MedicalHistory;
            if (fieldName == "BloodGroup") return patient.BloodGroup;
            if (fieldName == "Gender") return patient.Gender;
            if (fieldName == "DateOfBirth") return patient.DateOfBirth.ToString("yyyy-MM-dd");
            return null;
        }

        private static string NormalizeRequestedValue(string fieldName, string requestedValue)
        {
            if (string.IsNullOrWhiteSpace(requestedValue)) return null;

            if (fieldName != "DateOfBirth") return requestedValue.Trim();

            if (!DateTime.TryParse(requestedValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate))
                return null;

            return parsedDate.Date.ToString("yyyy-MM-dd");
        }

        private static void ApplyPatientFieldValue(Patient patient, string fieldName, string requestedValue)
        {
            if (fieldName == "MedicalHistory") patient.MedicalHistory = requestedValue;
            if (fieldName == "BloodGroup") patient.BloodGroup = requestedValue;
            if (fieldName == "Gender") patient.Gender = requestedValue;
            if (fieldName == "DateOfBirth") patient.DateOfBirth = DateTime.Parse(requestedValue, CultureInfo.InvariantCulture);
        }

        private static CorrectionRequestDto ToDto(PatientCorrectionRequest request)
        {
            return ToDto(request, request.Patient);
        }

        private static CorrectionRequestDto ToDto(PatientCorrectionRequest request, Patient patient)
        {
            return new CorrectionRequestDto
            {
                Id = request.Id,
                PatientId = request.PatientId,
                PatientName = patient?.User?.FullName,
                FieldName = request.FieldName,
                CurrentValue = request.CurrentValue,
                RequestedValue = request.RequestedValue,
                Reason = request.Reason,
                Status = request.Status,
                SubmittedAt = request.SubmittedAt,
                ReviewedAt = request.ReviewedAt,
                ReviewedByUserId = request.ReviewedByUserId,
                ReviewedByName = request.ReviewedByUser?.FullName,
                ReviewNote = request.ReviewNote
            };
        }
    }

    public class CorrectionRequestCreateModel
    {
        public string FieldName { get; set; }
        public string RequestedValue { get; set; }
        public string Reason { get; set; }
    }

    public class CorrectionRequestReviewModel
    {
        public string ReviewNote { get; set; }
    }

    public class CorrectionRequestDto
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; }
        public string FieldName { get; set; }
        public string CurrentValue { get; set; }
        public string RequestedValue { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string ReviewedByUserId { get; set; }
        public string ReviewedByName { get; set; }
        public string ReviewNote { get; set; }
    }
}
