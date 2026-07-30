using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SHMS.Backend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SHMS.Backend.Data
{
    public static class DbSeeder
    {
        private const string DemoPassword = "StrongPass123!";

        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<SHMSDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // 1. Seed Roles
            string[] roles = { "Admin", "Doctor", "Nurse", "Patient" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Seed Admin User
            var adminUser = await userManager.FindByNameAsync("admin");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "admin@shms.com",
                    FullName = "Hospital System Administrator",
                    Role = "Admin"
                };
                await userManager.CreateAsync(adminUser, DemoPassword);
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
            await EnsureDemoPasswordAsync(userManager, adminUser);

            // 3. Seed Doctors (6 Doctors)
            var doctorsData = new List<(string Username, string Name, string Email, string Specialty, string Roster)>
            {
                ("drsmith", "Dr. Alexander Smith", "drsmith@shms.com", "Cardiologist", "Mon-Wed-Fri 9AM-4PM"),
                ("drjones", "Dr. Sarah Jones", "drjones@shms.com", "Pediatrician", "Tue-Thu 10AM-6PM"),
                ("drwilliams", "Dr. David Williams", "drwilliams@shms.com", "Neurologist", "Mon-Tue-Thu 9AM-5PM"),
                ("drbrown", "Dr. Emily Brown", "drbrown@shms.com", "Dermatologist", "Wed-Fri 10AM-4PM"),
                ("drmiller", "Dr. Robert Miller", "drmiller@shms.com", "General Surgeon", "Tue-Wed-Fri 8AM-3PM"),
                ("drtaylor", "Dr. Jessica Taylor", "drtaylor@shms.com", "Oncologist", "Mon-Thu 9AM-5PM")
            };

            var doctorEntities = new List<Doctor>();

            foreach (var doc in doctorsData)
            {
                var docUser = await userManager.FindByNameAsync(doc.Username);
                if (docUser == null)
                {
                    docUser = new ApplicationUser
                    {
                        UserName = doc.Username,
                        Email = doc.Email,
                        FullName = doc.Name,
                        Role = "Doctor"
                    };
                    var result = await userManager.CreateAsync(docUser, DemoPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(docUser, "Doctor");
                        var doctor = new Doctor
                        {
                            UserId = docUser.Id,
                            Specialization = doc.Specialty,
                            RosterSchedule = doc.Roster,
                            IsAvailable = true
                        };
                        context.Doctors.Add(doctor);
                        doctorEntities.Add(doctor);
                    }
                }
                else
                {
                    var existingDoc = context.Doctors.FirstOrDefault(d => d.UserId == docUser.Id);
                    if (existingDoc != null) doctorEntities.Add(existingDoc);
                }

                await EnsureDemoPasswordAsync(userManager, docUser);
            }

            await context.SaveChangesAsync();

            // 4. Seed Nurses (4 Nurses)
            var nursesData = new List<(string Username, string Name, string Email, string Department, string Shift)>
            {
                ("nurse1", "Nurse Claire Dupont", "claire.dupont@shms.com", "General Ward", "Morning"),
                ("nurse2", "Nurse Kevin Patel", "kevin.patel@shms.com", "ICU", "Evening"),
                ("nurse3", "Nurse Maria Santos", "maria.santos@shms.com", "Pediatrics", "Morning"),
                ("nurse4", "Nurse James Okafor", "james.okafor@shms.com", "General Ward", "Night"),
            };

            var nurseEntities = new List<Nurse>();

            foreach (var nur in nursesData)
            {
                var nurUser = await userManager.FindByNameAsync(nur.Username);
                if (nurUser == null)
                {
                    nurUser = new ApplicationUser
                    {
                        UserName = nur.Username,
                        Email = nur.Email,
                        FullName = nur.Name,
                        Role = "Nurse"
                    };
                    var result = await userManager.CreateAsync(nurUser, DemoPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(nurUser, "Nurse");
                        var nurse = new Nurse
                        {
                            UserId = nurUser.Id,
                            Department = nur.Department,
                            Shift = nur.Shift,
                            IsAvailable = true
                        };
                        context.Nurses.Add(nurse);
                        nurseEntities.Add(nurse);
                    }
                }
                else
                {
                    var existingNurse = context.Nurses.FirstOrDefault(n => n.UserId == nurUser.Id);
                    if (existingNurse != null) nurseEntities.Add(existingNurse);
                }

                await EnsureDemoPasswordAsync(userManager, nurUser);
            }

            await context.SaveChangesAsync();

            // 5. Seed Patients (10 Patients)
            var patientsData = new List<(string Username, string Name, string Email, string Blood, string Gender, DateTime DOB, string History)>
            {
                ("patient1", "John Doe", "johndoe@gmail.com", "O-", "Male", new DateTime(1990, 5, 15), "Chronic hypertension, seasonal pollen allergies. Underwent appendectomy in 2021."),
                ("patient2", "Jane Miller", "janemiller@gmail.com", "A+", "Female", new DateTime(1985, 8, 22), "Diabetes Type II diagnosed in 2022. Managed via diet and metformin."),
                ("patient3", "Michael Johnson", "mjohnson@gmail.com", "B-", "Male", new DateTime(1978, 11, 3), "Moderate asthma since childhood. Prescribed albuterol inhaler."),
                ("patient4", "Emily Davis", "edavis@gmail.com", "AB+", "Female", new DateTime(1995, 4, 10), "No chronic conditions. Penicillin allergy reported."),
                ("patient5", "David Wilson", "dwilson@gmail.com", "O+", "Male", new DateTime(1962, 12, 5), "Coronary artery disease, high cholesterol. Prev coronary angioplasty in 2023."),
                ("patient6", "Sarah Martinez", "smartinez@gmail.com", "A-", "Female", new DateTime(2001, 9, 18), "Chronic migraines triggered by light. Managed with triptans."),
                ("patient7", "James Anderson", "janderson@gmail.com", "O+", "Male", new DateTime(1989, 2, 14), "Gastroesophageal reflux disease (GERD). Daily antacid management."),
                ("patient8", "Lisa Thomas", "lthomas@gmail.com", "B+", "Female", new DateTime(1993, 7, 29), "Hypothyroidism diagnosed in 2020. Active levothyroxine prescription."),
                ("patient9", "Robert Taylor", "rtaylor@gmail.com", "AB-", "Male", new DateTime(1955, 6, 25), "Rheumatoid arthritis affecting hands and knees. Bi-weekly physical therapy."),
                ("patient10", "Mary Jackson", "mjackson@gmail.com", "O-", "Female", new DateTime(2005, 10, 12), "No chronic conditions. Fit and healthy.")
            };

            var patientEntities = new List<Patient>();

            foreach (var pat in patientsData)
            {
                var patUser = await userManager.FindByNameAsync(pat.Username);
                if (patUser == null)
                {
                    patUser = new ApplicationUser
                    {
                        UserName = pat.Username,
                        Email = pat.Email,
                        FullName = pat.Name,
                        Role = "Patient"
                    };
                    var result = await userManager.CreateAsync(patUser, DemoPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(patUser, "Patient");
                        var patient = new Patient
                        {
                            UserId = patUser.Id,
                            BloodGroup = pat.Blood,
                            Gender = pat.Gender,
                            DateOfBirth = pat.DOB,
                            MedicalHistory = pat.History
                        };
                        context.Patients.Add(patient);
                        patientEntities.Add(patient);
                    }
                }
                else
                {
                    var existingPat = context.Patients.FirstOrDefault(p => p.UserId == patUser.Id);
                    if (existingPat != null) patientEntities.Add(existingPat);
                }

                await EnsureDemoPasswordAsync(userManager, patUser);
            }

            await context.SaveChangesAsync();

            // 6. Seed Beds if none exist
            if (!context.Beds.Any())
            {
                for (int i = 1; i <= 5; i++)
                    context.Beds.Add(new Bed { RoomNumber = $"10{i}", WardType = "General", IsOccupied = false });
                for (int i = 1; i <= 3; i++)
                    context.Beds.Add(new Bed { RoomNumber = $"20{i}", WardType = "ICU", IsOccupied = false });
                for (int i = 1; i <= 2; i++)
                    context.Beds.Add(new Bed { RoomNumber = $"30{i}", WardType = "Pediatric", IsOccupied = false });
                await context.SaveChangesAsync();

                var demoGeneralBed = await context.Beds.FirstOrDefaultAsync(b => b.WardType == "General");
                var demoIcuBed = await context.Beds.FirstOrDefaultAsync(b => b.WardType == "ICU");

                if (demoGeneralBed != null && patientEntities.Count > 0)
                {
                    demoGeneralBed.IsOccupied = true;
                    demoGeneralBed.PatientId = patientEntities[0].Id;
                }
                if (demoIcuBed != null && patientEntities.Count > 4)
                {
                    demoIcuBed.IsOccupied = true;
                    demoIcuBed.PatientId = patientEntities[4].Id;
                }
                await context.SaveChangesAsync();
            }

            // 7. Seed Inventory Items if none exist
            if (!context.InventoryItems.Any())
            {
                context.InventoryItems.Add(new InventoryItem { Name = "Paracetamol 500mg", Category = "Medicine", Quantity = 150, ThresholdValue = 50, Price = 0.50m });
                context.InventoryItems.Add(new InventoryItem { Name = "Amoxicillin 250mg", Category = "Medicine", Quantity = 18, ThresholdValue = 30, Price = 1.80m });
                context.InventoryItems.Add(new InventoryItem { Name = "IV Infusion Kits", Category = "Consumable", Quantity = 45, ThresholdValue = 20, Price = 12.00m });
                context.InventoryItems.Add(new InventoryItem { Name = "Oxygen Cylinder (Standard)", Category = "Equipment", Quantity = 4, ThresholdValue = 5, Price = 150.00m });
                context.InventoryItems.Add(new InventoryItem { Name = "Sterile Syringes (Box of 100)", Category = "Consumable", Quantity = 250, ThresholdValue = 50, Price = 8.50m });
                await context.SaveChangesAsync();
            }

            if (!context.HospitalServices.Any())
            {
                context.HospitalServices.Add(new HospitalService { Name = "Consultation", Category = "Appointment", Price = 100.00m, IsActive = true });
                context.HospitalServices.Add(new HospitalService { Name = "Follow-up Consultation", Category = "Appointment", Price = 60.00m, IsActive = true });
                context.HospitalServices.Add(new HospitalService { Name = "Blood Test", Category = "Lab", Price = 35.00m, IsActive = true });
                context.HospitalServices.Add(new HospitalService { Name = "X-Ray", Category = "Radiology", Price = 80.00m, IsActive = true });
                context.HospitalServices.Add(new HospitalService { Name = "Emergency Care", Category = "Emergency", Price = 250.00m, IsActive = true });
                await context.SaveChangesAsync();
            }

            // 8. Seed Sample Appointments and Bills
            if (!context.Appointments.Any() && doctorEntities.Count >= 2 && patientEntities.Count >= 3)
            {
                var app1 = new Appointment
                {
                    PatientId = patientEntities[0].Id,
                    DoctorId = doctorEntities[0].Id,
                    AppointmentDate = DateTime.Now.AddDays(-5),
                    Status = "Completed",
                    Symptoms = "Mild chest pains, palpitations after running.",
                    Diagnosis = "Mild sinus tachycardia triggered by physical fatigue. Blood pressure slightly elevated at 135/85.",
                    Prescription = "Paracetamol 500mg as needed for fatigue.\nRest 8 hours daily.\nLimit caffeine intake."
                };
                context.Appointments.Add(app1);

                var app2 = new Appointment
                {
                    PatientId = patientEntities[1].Id,
                    DoctorId = doctorEntities[1].Id,
                    AppointmentDate = DateTime.Now.AddDays(-3),
                    Status = "Completed",
                    Symptoms = "Sore throat and dry cough.",
                    Diagnosis = "Acute viral pharyngitis.",
                    Prescription = "Amoxicillin 250mg - 3 times daily for 5 days.\nThroat lozenges as needed."
                };
                context.Appointments.Add(app2);

                var app3 = new Appointment
                {
                    PatientId = patientEntities[2].Id,
                    DoctorId = doctorEntities[2].Id,
                    AppointmentDate = DateTime.Now.AddDays(2),
                    Status = "Scheduled",
                    Symptoms = "Numbness in left fingers and tension headaches.",
                    Diagnosis = "",
                    Prescription = ""
                };
                context.Appointments.Add(app3);

                var app4 = new Appointment
                {
                    PatientId = patientEntities[4].Id,
                    DoctorId = doctorEntities[0].Id,
                    AppointmentDate = DateTime.Now.AddDays(3),
                    Status = "Scheduled",
                    Symptoms = "Follow up check for high cholesterol and arterial stent validation.",
                    Diagnosis = "",
                    Prescription = ""
                };
                context.Appointments.Add(app4);

                var app5 = new Appointment
                {
                    PatientId = patientEntities[5].Id,
                    DoctorId = doctorEntities[3].Id,
                    AppointmentDate = DateTime.Now.AddHours(18),
                    Status = "Scheduled",
                    Symptoms = "Recurring skin irritation and sleep disruption.",
                    Diagnosis = "",
                    Prescription = ""
                };
                context.Appointments.Add(app5);

                await context.SaveChangesAsync();

                var consultation = await context.HospitalServices.FirstAsync(s => s.Name == "Consultation");

                var seededBills = new List<Bill>
                {
                    CreateSeedBill(patientEntities[0].Id, app1.Id, consultation, "Paid", DateTime.Now.AddDays(-5), "INV-SEED-00001"),
                    CreateSeedBill(patientEntities[1].Id, app2.Id, consultation, "Paid", DateTime.Now.AddDays(-3), "INV-SEED-00002"),
                    CreateSeedBill(patientEntities[2].Id, app3.Id, consultation, "Pending", DateTime.Now.AddDays(2), "INV-SEED-00003"),
                    CreateSeedBill(patientEntities[4].Id, app4.Id, consultation, "Pending", DateTime.Now.AddDays(3), "INV-SEED-00004"),
                    CreateSeedBill(patientEntities[5].Id, app5.Id, consultation, "Pending", DateTime.Now.AddHours(18), "INV-SEED-00005"),
                    new Bill
                    {
                        PatientId = patientEntities[0].Id,
                        AppointmentId = null,
                        InvoiceNumber = "INV-SEED-00006",
                        Subtotal = 350.00m,
                        DiscountAmount = 0,
                        TaxAmount = 0,
                        Amount = 350.00m,
                        PaymentStatus = "Paid",
                        DateGenerated = DateTime.Now.AddDays(-2),
                        DatePaid = DateTime.Now.AddDays(-2),
                        Notes = "Manual seed invoice",
                        Items = new List<BillItem>
                        {
                            new BillItem
                            {
                                Description = "Manual hospital service",
                                Quantity = 1,
                                UnitPrice = 350.00m,
                                LineTotal = 350.00m
                            }
                        }
                    }
                };

                context.Bills.AddRange(seededBills);
                await context.SaveChangesAsync();
            }

            if (!context.Expenses.Any())
            {
                context.Expenses.Add(new Expense { Title = "Medical supplies restock", Category = "Inventory", Amount = 420.00m, ExpenseDate = DateTime.Now.AddDays(-7), Notes = "Basic consumables", CreatedAt = DateTime.Now.AddDays(-7) });
                context.Expenses.Add(new Expense { Title = "Ward equipment maintenance", Category = "Maintenance", Amount = 180.00m, ExpenseDate = DateTime.Now.AddDays(-4), Notes = "Routine equipment service", CreatedAt = DateTime.Now.AddDays(-4) });
                context.Expenses.Add(new Expense { Title = "Cleaning services", Category = "Operations", Amount = 95.00m, ExpenseDate = DateTime.Now.AddDays(-1), Notes = "Daily cleaning support", CreatedAt = DateTime.Now.AddDays(-1) });
                await context.SaveChangesAsync();
            }

            // 9. Seed Care Instructions (only if nurses and doctors exist)
            if (!context.CareInstructions.Any() && nurseEntities.Count >= 2 && doctorEntities.Count >= 2 && patientEntities.Count >= 3)
            {
                context.CareInstructions.Add(new CareInstruction
                {
                    PatientId = patientEntities[0].Id,
                    DoctorId = doctorEntities[0].Id,
                    NurseId = nurseEntities[0].Id,
                    Instructions = "Monitor blood pressure every 2 hours. Administer Paracetamol 500mg if patient reports pain above 6/10. Ensure patient rests and is not disturbed between 2PM–4PM.",
                    Priority = "High",
                    Status = "InProgress",
                    CreatedAt = DateTime.Now.AddHours(-6),
                    UpdatedAt = DateTime.Now.AddHours(-2),
                    NurseNotes = "Patient is cooperative. BP readings: 130/82 at 10AM, 128/80 at 12PM. Trending down."
                });

                context.CareInstructions.Add(new CareInstruction
                {
                    PatientId = patientEntities[4].Id,
                    DoctorId = doctorEntities[0].Id,
                    NurseId = nurseEntities[1].Id,
                    Instructions = "Patient is post-angioplasty. Monitor O2 saturation continuously. Alert Dr. Smith immediately if SpO2 drops below 92%. Ensure patient does not leave bed unassisted.",
                    Priority = "Critical",
                    Status = "InProgress",
                    CreatedAt = DateTime.Now.AddHours(-12),
                    UpdatedAt = DateTime.Now.AddHours(-1),
                    NurseNotes = "SpO2 stable at 96-97%. Patient is alert and comfortable."
                });

                context.CareInstructions.Add(new CareInstruction
                {
                    PatientId = patientEntities[1].Id,
                    DoctorId = doctorEntities[1].Id,
                    NurseId = nurseEntities[0].Id,
                    Instructions = "Administer Amoxicillin 250mg with meals at 8AM, 1PM, and 7PM. Encourage fluid intake (min 2L/day). Check temperature every 4 hours.",
                    Priority = "Medium",
                    Status = "Completed",
                    CreatedAt = DateTime.Now.AddDays(-2),
                    UpdatedAt = DateTime.Now.AddDays(-1),
                    NurseNotes = "All medication doses administered on schedule. Patient fever resolved. Discharged."
                });

                context.CareInstructions.Add(new CareInstruction
                {
                    PatientId = patientEntities[2].Id,
                    DoctorId = doctorEntities[2].Id,
                    NurseId = nurseEntities[1].Id,
                    Instructions = "Patient reports tension headaches. Apply cold compress to forehead PRN. Dim room lighting. Do not allow screen use (tablet/phone). Reassess in 3 hours.",
                    Priority = "Medium",
                    Status = "Pending",
                    CreatedAt = DateTime.Now.AddHours(-1),
                    UpdatedAt = DateTime.Now.AddHours(-1),
                    NurseNotes = ""
                });

                context.CareInstructions.Add(new CareInstruction
                {
                    PatientId = patientEntities[5].Id,
                    DoctorId = doctorEntities[3].Id,
                    NurseId = nurseEntities[2].Id,
                    Instructions = "Apply prescribed topical cream (Hydrocortisone 1%) to affected area on left forearm twice daily. Document skin condition with photos for Dr. Brown's review. Use hypoallergenic gloves when handling.",
                    Priority = "Low",
                    Status = "Pending",
                    CreatedAt = DateTime.Now.AddMinutes(-30),
                    UpdatedAt = DateTime.Now.AddMinutes(-30),
                    NurseNotes = ""
                });

                await context.SaveChangesAsync();
            }

            // 10. Seed Patient Notifications
            if (!context.PatientNotifications.Any() && patientEntities.Count >= 3 && doctorEntities.Count >= 2)
            {
                var drSmithUser = await userManager.FindByNameAsync("drsmith");
                var drJonesUser = await userManager.FindByNameAsync("drjones");
                var nurse1User = await userManager.FindByNameAsync("nurse1");

                if (drSmithUser != null)
                {
                    context.PatientNotifications.Add(new PatientNotification
                    {
                        PatientId = patientEntities[0].Id,
                        SenderId = drSmithUser.Id,
                        SenderName = "Dr. Alexander Smith",
                        SenderRole = "Doctor",
                        Subject = "Post-Visit Precautions — Cardiac Care",
                        Message = "Dear John,\n\nFollowing your appointment, please observe the following precautions:\n\n1. Avoid strenuous physical activity for the next 2 weeks.\n2. Take your prescribed Paracetamol only when pain exceeds 6/10 — do not exceed 4 tablets/day.\n3. Monitor your blood pressure daily and log readings.\n4. Reduce sodium intake — aim for under 1500mg/day.\n5. Call the clinic immediately if you experience shortness of breath or chest pressure.\n\nStay well,\nDr. Alexander Smith, Cardiologist",
                        NotificationType = "Precaution",
                        IsRead = false,
                        SentAt = DateTime.Now.AddHours(-4),
                        ScheduledFor = null,
                        IsEmailSent = true
                    });

                    context.PatientNotifications.Add(new PatientNotification
                    {
                        PatientId = patientEntities[4].Id,
                        SenderId = drSmithUser.Id,
                        SenderName = "Dr. Alexander Smith",
                        SenderRole = "Doctor",
                        Subject = "Upcoming Follow-Up Appointment Reminder",
                        Message = "Dear David,\n\nThis is a reminder that you have a follow-up appointment scheduled in 3 days to review your cardiac stent and cholesterol levels.\n\nPlease:\n- Fast for 8 hours before the appointment (water is fine)\n- Bring your home blood pressure log\n- Bring your current medication list\n\nSee you soon,\nDr. Alexander Smith",
                        NotificationType = "Checkup",
                        IsRead = true,
                        SentAt = DateTime.Now.AddDays(-1),
                        ScheduledFor = null,
                        IsEmailSent = true
                    });
                }

                if (drJonesUser != null)
                {
                    context.PatientNotifications.Add(new PatientNotification
                    {
                        PatientId = patientEntities[1].Id,
                        SenderId = drJonesUser.Id,
                        SenderName = "Dr. Sarah Jones",
                        SenderRole = "Doctor",
                        Subject = "Recovery Update — You're Improving!",
                        Message = "Hi Jane,\n\nGreat news! Your throat swab results came back and the viral infection is clearing well.\n\nYour recovery is on track. Please continue:\n- Finishing the full antibiotic course (2 more days)\n- Drinking warm fluids\n- Getting adequate rest\n\nYou should feel significantly better by the weekend. If symptoms worsen or you develop a fever above 38.5°C, please contact us.\n\nTake care,\nDr. Sarah Jones",
                        NotificationType = "Recovery",
                        IsRead = false,
                        SentAt = DateTime.Now.AddHours(-1),
                        ScheduledFor = null,
                        IsEmailSent = true
                    });
                }

                if (nurse1User != null)
                {
                    context.PatientNotifications.Add(new PatientNotification
                    {
                        PatientId = patientEntities[0].Id,
                        SenderId = nurse1User.Id,
                        SenderName = "Nurse Claire Dupont",
                        SenderRole = "Nurse",
                        Subject = "Daily Vitals Check — Action Required",
                        Message = "Hi John,\n\nThis is a scheduled reminder to measure and log your blood pressure today.\n\nYour target BP range is below 130/80 mmHg.\n\nPlease use your home BP monitor at:\n- Morning (before breakfast)\n- Evening (before dinner)\n\nYou can share your readings by replying to this message or at your next visit.\n\nBest,\nNurse Claire Dupont, General Ward",
                        NotificationType = "General",
                        IsRead = false,
                        SentAt = DateTime.Now.AddMinutes(-45),
                        ScheduledFor = null,
                        IsEmailSent = true
                    });

                    // A future scheduled notification
                    context.PatientNotifications.Add(new PatientNotification
                    {
                        PatientId = patientEntities[0].Id,
                        SenderId = nurse1User.Id,
                        SenderName = "Nurse Claire Dupont",
                        SenderRole = "Nurse",
                        Subject = "Scheduled: Weekly Medication Reminder",
                        Message = "Hi John,\n\nThis is your weekly reminder to pick up your Paracetamol prescription from the pharmacy.\n\nAlso please do not skip your rest schedule this weekend.\n\nBest,\nNurse Claire Dupont",
                        NotificationType = "Precaution",
                        IsRead = false,
                        SentAt = DateTime.Now,
                        ScheduledFor = DateTime.Now.AddDays(7),
                        IsEmailSent = false
                    });
                }

                await context.SaveChangesAsync();
            }
        }

        private static Bill CreateSeedBill(int patientId, int appointmentId, HospitalService service, string status, DateTime dateGenerated, string invoiceNumber)
        {
            return new Bill
            {
                PatientId = patientId,
                AppointmentId = appointmentId,
                InvoiceNumber = invoiceNumber,
                Subtotal = service.Price,
                DiscountAmount = 0,
                TaxAmount = 0,
                Amount = service.Price,
                PaymentStatus = status,
                DateGenerated = dateGenerated,
                DatePaid = status == "Paid" ? dateGenerated : (DateTime?)null,
                Notes = "Seed appointment invoice",
                Items = new List<BillItem>
                {
                    new BillItem
                    {
                        HospitalServiceId = service.Id,
                        Description = service.Name,
                        Quantity = 1,
                        UnitPrice = service.Price,
                        LineTotal = service.Price
                    }
                }
            };
        }

        private static async Task EnsureDemoPasswordAsync(UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            if (user == null) return;

            if (await userManager.HasPasswordAsync(user))
            {
                await userManager.RemovePasswordAsync(user);
            }

            await userManager.AddPasswordAsync(user, DemoPassword);
        }
    }
}
