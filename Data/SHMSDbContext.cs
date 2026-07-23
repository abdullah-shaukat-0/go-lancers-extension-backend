using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SHMS.Backend.Models;

namespace SHMS.Backend.Data
{
    public class SHMSDbContext : IdentityDbContext<ApplicationUser>
    {
        public SHMSDbContext(DbContextOptions<SHMSDbContext> options) : base(options)
        {
        }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Nurse> Nurses { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Bed> Beds { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<Bill> Bills { get; set; }
        public DbSet<CareInstruction> CareInstructions { get; set; }
        public DbSet<PatientNotification> PatientNotifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Patient
            builder.Entity<Patient>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Doctor
            builder.Entity<Doctor>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Nurse
            builder.Entity<Nurse>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Appointment
            builder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany()
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany()
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Bed
            builder.Entity<Bed>()
                .HasOne(b => b.Patient)
                .WithMany()
                .HasForeignKey(b => b.PatientId)
                .OnDelete(DeleteBehavior.SetNull);

            // Bill
            builder.Entity<Bill>()
                .HasOne(b => b.Patient)
                .WithMany()
                .HasForeignKey(b => b.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            // CareInstruction
            builder.Entity<CareInstruction>()
                .HasOne(ci => ci.Patient)
                .WithMany()
                .HasForeignKey(ci => ci.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CareInstruction>()
                .HasOne(ci => ci.Doctor)
                .WithMany()
                .HasForeignKey(ci => ci.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CareInstruction>()
                .HasOne(ci => ci.Nurse)
                .WithMany()
                .HasForeignKey(ci => ci.NurseId)
                .OnDelete(DeleteBehavior.Restrict);

            // PatientNotification
            builder.Entity<PatientNotification>()
                .HasOne(pn => pn.Patient)
                .WithMany()
                .HasForeignKey(pn => pn.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // AuditLog — standalone, no navigation properties, fully immutable
            builder.Entity<AuditLog>()
                .HasKey(a => a.Id);
        }
    }
}
