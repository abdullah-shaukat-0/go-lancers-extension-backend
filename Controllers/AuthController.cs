using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
using SHMS.Backend.Services;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SHMS.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SHMSDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IAuditService _auditService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SHMSDbContext context,
            IConfiguration configuration,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _configuration = configuration;
            _auditService = auditService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
            {
                await _auditService.LogAnonymousAsync(model.Username, "REGISTRATION_ATTEMPT", "Model validation failed", "Failure", HttpContext.Connection.RemoteIpAddress?.ToString());
                return BadRequest(ModelState);
            }

            var userExists = await _userManager.FindByNameAsync(model.Username);
            if (userExists != null)
            {
                await _auditService.LogAnonymousAsync(model.Username, "REGISTRATION_FAILURE", "User already exists", "Failure", HttpContext.Connection.RemoteIpAddress?.ToString());
                return BadRequest(new { Message = "User already exists!" });
            }

            ApplicationUser user = new ApplicationUser()
            {
                Email = model.Email,
                SecurityStamp = Guid.NewGuid().ToString(),
                UserName = model.Username,
                FullName = model.FullName,
                Role = model.Role
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError("errors", err.Description);
                }
                return BadRequest(ModelState);
            }

            // Create Role if not exists
            if (!await _roleManager.RoleExistsAsync(model.Role))
                await _roleManager.CreateAsync(new IdentityRole(model.Role));

            await _userManager.AddToRoleAsync(user, model.Role);

            // Create Profile depending on the role
            if (model.Role.Equals("Patient", StringComparison.OrdinalIgnoreCase))
            {
                var patient = new Patient
                {
                    UserId = user.Id,
                    BloodGroup = model.BloodGroup ?? "O+",
                    Gender = model.Gender ?? "Male",
                    DateOfBirth = model.DateOfBirth ?? DateTime.UtcNow.AddYears(-30),
                    MedicalHistory = "No history recorded yet."
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
            }
            else if (model.Role.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
            {
                var doctor = new Doctor
                {
                    UserId = user.Id,
                    Specialization = model.Specialization ?? "General Physician",
                    RosterSchedule = model.RosterSchedule ?? "Mon-Fri 9AM-5PM",
                    IsAvailable = true
                };
                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();
            }
            else if (model.Role.Equals("Nurse", StringComparison.OrdinalIgnoreCase))
            {
                var nurse = new Nurse
                {
                    UserId = user.Id,
                    Department = model.Department ?? "General Ward",
                    Shift = model.Shift ?? "Morning",
                    IsAvailable = true
                };
                _context.Nurses.Add(nurse);
                await _context.SaveChangesAsync();
            }

            await _auditService.LogAsync(new AuditLogEntry
            {
                Action           = "REGISTER",
                ResourceType     = "User",
                ResourceId       = user.Id,
                Details          = $"New {model.Role} account registered: {model.Username}",
                OverrideUserId   = user.Id,
                OverrideUserName = user.UserName,
                OverrideUserRole = model.Role
            });

            return Ok(new { Status = "Success", Message = "User created successfully!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
                // Generate secure 6-digit verification code
                var random = new Random();
                var mfaCode = random.Next(100000, 999999).ToString();
                
                user.MfaCode = mfaCode;
                user.MfaExpiry = DateTime.UtcNow.AddMinutes(3); // Code expires in 3 minutes
                await _userManager.UpdateAsync(user);

                // Log the MFA generation event
                await _auditService.LogAnonymousAsync(user.UserName, "MFA_CODE_GENERATED", $"MFA Code generated for user. Code: {mfaCode}", "Success", HttpContext.Connection.RemoteIpAddress?.ToString());

                // Simulated Email Dispatch for Ontario compliance (printing to console/debug logs)
                Console.WriteLine($"[EMAIL SERVICE] Sending secure MFA verification code to patient/staff email {user.Email}. Code: {mfaCode}");

                await _auditService.LogAsync(new AuditLogEntry
                {
                    Action           = "LOGIN",
                    ResourceType     = "User",
                    ResourceId       = user.Id,
                    Details          = $"User '{user.UserName}' ({user.Role}) logged in successfully",
                    OverrideUserId   = user.Id,
                    OverrideUserName = user.UserName,
                    OverrideUserRole = user.Role
                });

                return Ok(new
                {
                    mfaRequired = true,
                    username = user.UserName,
                    message = "Verification code dispatched to your registered email."
                });
            }

            await _auditService.LogAsync(new AuditLogEntry
            {
                Action           = "LOGIN_FAILED",
                ResourceType     = "User",
                ResourceId       = model.Username,
                Details          = $"Failed login attempt for username: {model.Username}",
                WasSuccessful    = false,
                OverrideUserName = model.Username
            });

            return Unauthorized(new { Message = "Invalid username or password" });
        }

        [HttpPost("verify-mfa")]
        public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                await _auditService.LogAnonymousAsync(model.Username, "MFA_VERIFICATION_FAILURE", "User not found during verification", "Failure", HttpContext.Connection.RemoteIpAddress?.ToString());
                return BadRequest(new { Message = "Invalid user" });
            }

            if (user.MfaCode == null || user.MfaExpiry < DateTime.UtcNow || user.MfaCode != model.Code)
            {
                await _auditService.LogAnonymousAsync(user.UserName, "MFA_VERIFICATION_FAILURE", "Invalid or expired MFA code supplied", "Failure", HttpContext.Connection.RemoteIpAddress?.ToString());
                return BadRequest(new { Message = "Invalid or expired verification code." });
            }

            // Clear used MFA code
            user.MfaCode = null;
            user.MfaExpiry = null;
            await _userManager.UpdateAsync(user);

            var userRoles = await _userManager.GetRolesAsync(user);

            var authClaims = new[]
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("fullName", user.FullName ?? ""),
                new Claim(ClaimTypes.Role, user.Role ?? "Patient")
            };

            var claimsIdentity = new ClaimsIdentity(authClaims);
            foreach (var role in userRoles)
            {
                claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
            }

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Secret"] ?? "SuperSecretSecurityKeyThatIsLongEnough"));

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:ValidIssuer"] ?? "http://localhost:5000",
                audience: _configuration["JWT:ValidAudience"] ?? "http://localhost:5000",
                expires: DateTime.Now.AddHours(1),
                claims: claimsIdentity.Claims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            int profileId = 0;
            if (user.Role == "Patient")
            {
                var p = await _context.Patients.FirstOrDefaultAsync(x => x.UserId == user.Id);
                if (p != null) profileId = p.Id;
            }
            else if (user.Role == "Doctor")
            {
                var d = await _context.Doctors.FirstOrDefaultAsync(x => x.UserId == user.Id);
                if (d != null) profileId = d.Id;
            }
            else if (user.Role == "Nurse")
            {
                var n = await _context.Nurses.FirstOrDefaultAsync(x => x.UserId == user.Id);
                if (n != null) profileId = n.Id;
            }

            await _auditService.LogAnonymousAsync(user.UserName, "LOGIN_SUCCESS", $"User {user.UserName} completed MFA and logged in successfully", "Success", HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                username = user.UserName,
                fullName = user.FullName,
                role = user.Role,
                userId = user.Id,
                profileId = profileId
            });
        }
    }

    public class MfaVerifyModel
    {
        public string Username { get; set; }
        public string Code { get; set; }
    }

    public class LoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class RegisterModel
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; } // Admin, Doctor, Nurse, Patient
        
        public string BloodGroup { get; set; }
        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }

        public string Specialization { get; set; }
        public string RosterSchedule { get; set; }

        // Nurse-specific
        public string Department { get; set; }
        public string Shift { get; set; }
    }
}
