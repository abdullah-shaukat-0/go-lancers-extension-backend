using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SHMS.Backend.Data;
using SHMS.Backend.Models;
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

        public AuthController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SHMSDbContext context,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userExists = await _userManager.FindByNameAsync(model.Username);
            if (userExists != null)
                return BadRequest(new { Message = "User already exists!" });

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

            return Ok(new { Status = "Success", Message = "User created successfully!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user != null && await _userManager.CheckPasswordAsync(user, model.Password))
            {
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
                    expires: DateTime.Now.AddHours(3),
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
            return Unauthorized(new { Message = "Invalid username or password" });
        }
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
