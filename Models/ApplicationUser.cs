using Microsoft.AspNetCore.Identity;

namespace SHMS.Backend.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string Role { get; set; } // Admin, Doctor, Nurse, Patient
        public string MfaCode { get; set; }
        public System.DateTime? MfaExpiry { get; set; }
    }
}
