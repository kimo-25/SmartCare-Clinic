using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;
using System.Security.Claims;

namespace SCMS.Controllers
{
    [Authorize(Roles = "Doctor,Admin")]
    public class DoctorController : Controller
    {
        private readonly AppDbContext _context;

        public DoctorController(AppDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId()
        {
            // 1) Session
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (int.TryParse(userIdStr, out var id) && id > 0)
                return id;

            // 2) Claims (Cookie)
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claimId, out var cid) ? cid : 0;
        }

        private UserType CurrentUserType()
        {
            // 1) Session
            var t = HttpContext.Session.GetInt32("UserType");
            if (t.HasValue) return (UserType)t.Value;

            // 2) Claims Role
            if (User.IsInRole("Admin")) return UserType.Admin;
            if (User.IsInRole("Doctor")) return UserType.Doctor;

            return UserType.User;
        }

        private IActionResult RequireDoctor()
        {
            // لو مش مسجل دخول (حتى مع Authorize، احتياط)
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToAction("Login", "Account");

            var userId = CurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            // انت كنت سامح Admin يدخل هنا — خليتها زي ما هي
            if (CurrentUserType() != UserType.Doctor && CurrentUserType() != UserType.Admin)
                return RedirectToAction("AccessDenied", "Account");

            return null!;
        }

        // Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var id = CurrentUserId();

            var doctor = await _context.Set<Doctor>()
                .Include(d => d.Feedbacks)
                .Include(d => d.Appointments)
                    .ThenInclude(a => a.Bookings)
                    .ThenInclude(b => b.Patient)
                .FirstOrDefaultAsync(d => d.UserId == id);

            if (doctor == null) return NotFound();

            var vm = new DoctorProfileVm
            {
                DoctorId = doctor.UserId,
                FullName = doctor.FullName,
                Specialization = doctor.Specialization,
                YearsOfExperience = doctor.YearsOfExperience,
                DepartmentName = doctor.DepartmentName,
                PhoneNumber = doctor.PhoneNumber,
                AverageRate = doctor.Feedbacks.Any() ? doctor.Feedbacks.Average(f => f.Rate) : 0,
                FeedbackCount = doctor.Feedbacks.Count,
                UpcomingAppointments = doctor.Appointments
                    .Where(a => a.AppointmentDate >= DateTime.Today)
                    .OrderBy(a => a.AppointmentDate)
                    .Select(a => new DoctorAppointmentVm
                    {
                        AppointmentId = a.AppointmentId,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Capacity = a.Capacity,
                        CurrentCount = a.CurrentCount,
                        Status = a.Status
                    }).ToList()
            };

            return View(vm);
        }

        // Appointments List
        public async Task<IActionResult> Appointments()
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var id = CurrentUserId();

            var doctor = await _context.Set<Doctor>()
                .Include(d => d.Appointments)
                .FirstOrDefaultAsync(d => d.UserId == id);

            if (doctor == null) return NotFound();

            var vm = new DoctorProfileVm
            {
                DoctorId = doctor.UserId,
                UpcomingAppointments = doctor.Appointments
                    .Where(a => a.AppointmentDate >= DateTime.Today)
                    .OrderBy(a => a.AppointmentDate)
                    .Select(a => new DoctorAppointmentVm
                    {
                        AppointmentId = a.AppointmentId,
                        AppointmentDate = a.AppointmentDate,
                        StartTime = a.StartTime,
                        EndTime = a.EndTime,
                        Capacity = a.Capacity,
                        CurrentCount = a.CurrentCount,
                        Status = a.Status
                    }).ToList()
            };

            return View(vm);
        }

        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var doctorId = CurrentUserId();

            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == id && a.DoctorId == doctorId);

            if (appointment == null) return NotFound();

            var vm = new DoctorAppointmentVm
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentDate = appointment.AppointmentDate,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                Capacity = appointment.Capacity,
                CurrentCount = appointment.CurrentCount,
                Status = appointment.Status
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult CreateAppointment()
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAppointment(DoctorAppointmentVm vm)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var doctorId = CurrentUserId();

            if (!ModelState.IsValid)
                return View(vm);

            var appointment = new Appointment
            {
                DoctorId = doctorId,
                AppointmentDate = vm.AppointmentDate,
                StartTime = vm.StartTime,
                EndTime = vm.EndTime,
                Capacity = vm.Capacity,
                CurrentCount = 0,
                Status = vm.Status
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            return RedirectToAction("Appointments");
        }

        [HttpGet]
        public IActionResult CreatePrescription(int patientId)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var doctorId = CurrentUserId();

            ViewBag.PatientId = patientId;
            ViewBag.DoctorId = doctorId;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePrescription(Prescription vm)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var doctorId = CurrentUserId();

            if (!ModelState.IsValid)
                return View(vm);

            vm.DoctorId = doctorId; // prevent tampering
            vm.CreatedAt = DateTime.UtcNow;

            _context.Prescriptions.Add(vm);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> MedicalRecords()
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var doctorId = CurrentUserId();

            var records = await _context.MedicalRecords
                .Include(r => r.Patient)
                .Include(r => r.RelatedPrescription)
                .Where(r => r.RelatedPrescription == null || r.RelatedPrescription.DoctorId == doctorId)
                .ToListAsync();

            return View(records);
        }

        public async Task<IActionResult> DetailsMedicalRecord(int id)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var doctorId = CurrentUserId();

            var record = await _context.MedicalRecords
                .Include(r => r.Patient)
                .Include(r => r.RelatedPrescription)
                .Include(r => r.RadiologyResult)
                .FirstOrDefaultAsync(r => r.RecordId == id);

            if (record == null) return NotFound();

            if (record.RelatedPrescription != null && record.RelatedPrescription.DoctorId != doctorId)
                return RedirectToAction("AccessDenied", "Account");

            return View(record);
        }

        [HttpGet]
        public async Task<IActionResult> EditMedicalRecord(int id)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var doctorId = CurrentUserId();

            var record = await _context.MedicalRecords
                .Include(r => r.RelatedPrescription)
                .FirstOrDefaultAsync(r => r.RecordId == id);

            if (record == null) return NotFound();

            if (record.RelatedPrescription != null && record.RelatedPrescription.DoctorId != doctorId)
                return RedirectToAction("AccessDenied", "Account");

            return View(record);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMedicalRecord(MedicalRecord vm)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var doctorId = CurrentUserId();

            if (!ModelState.IsValid)
                return View(vm);

            var record = await _context.MedicalRecords
                .Include(r => r.RelatedPrescription)
                .FirstOrDefaultAsync(r => r.RecordId == vm.RecordId);

            if (record == null) return NotFound();

            if (record.RelatedPrescription != null && record.RelatedPrescription.DoctorId != doctorId)
                return RedirectToAction("AccessDenied", "Account");

            record.Description = vm.Description;
            record.PrescriptionId = vm.PrescriptionId;
            record.RadiologyResultId = vm.RadiologyResultId;

            await _context.SaveChangesAsync();

            return RedirectToAction("MedicalRecords");
        }

        public async Task<IActionResult> PatientFile(int patientId)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == patientId);

            if (patient == null) return NotFound();

            var records = await _context.MedicalRecords
                .Include(r => r.RelatedPrescription)
                .Include(r => r.RadiologyResult)
                .Where(r => r.PatientId == patient.UserId)
                .OrderByDescending(r => r.RecordDate)
                .ToListAsync();

            var vm = new PatientProfileVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Age = patient.Age,
                Gender = patient.Gender,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary,
                Records = records.Select(r => new PatientProfileRecordVm
                {
                    RecordId = r.RecordId,
                    RecordDate = r.RecordDate,
                    Description = r.Description,
                    Diagnosis = r.RelatedPrescription?.Diagnosis,
                    Treatment = r.RelatedPrescription?.Treatment,
                    RadiologyTestName = r.RadiologyResult?.Report,
                    RadiologyStatus = r.RadiologyResult?.Status
                }).ToList()
            };

            return View(vm);
        }
    }
}
