using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;
using SCMS.ViewModels.Doctor;
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
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (int.TryParse(userIdStr, out var id) && id > 0)
                return id;

            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claimId, out var cid) ? cid : 0;
        }

        private UserType CurrentUserType()
        {
            var t = HttpContext.Session.GetInt32("UserType");
            if (t.HasValue) return (UserType)t.Value;

            if (User.IsInRole("Admin")) return UserType.Admin;
            if (User.IsInRole("Doctor")) return UserType.Doctor;

            return UserType.User;
        }

        private IActionResult? RequireDoctor()
        {
            if (!(User?.Identity?.IsAuthenticated ?? false))
                return RedirectToAction("Login", "Account");

            var userId = CurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Doctor && CurrentUserType() != UserType.Admin)
                return RedirectToAction("AccessDenied", "Account");

            return null;
        }

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

                IsProfileIncomplete =
                    string.IsNullOrWhiteSpace(doctor.PhoneNumber) ||
                    string.IsNullOrWhiteSpace(doctor.DepartmentName) ||
                    string.IsNullOrWhiteSpace(doctor.Specialization) ||
                    doctor.YearsOfExperience <= 0,

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
                        Status = a.Status,
                        Price = a.Price
                    }).ToList()
            };

            return View(vm);
        }

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
                        Status = a.Status,
                        Price = a.Price
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
                .Include(a => a.Bookings)
                    .ThenInclude(b => b.Patient)
                .FirstOrDefaultAsync(a => a.AppointmentId == id && a.DoctorId == doctorId);

            if (appointment == null) return NotFound();

            var patientIds = appointment.Bookings
                .Where(b => b.Patient != null)
                .Select(b => b.Patient.UserId)
                .Distinct()
                .ToList();

            var latestResults = await _context.RadiologyResults
                .Include(r => r.Request)
                .Where(r => r.Request.DoctorId == doctorId && patientIds.Contains(r.Request.PatientId))
                .OrderByDescending(r => r.ResultDate)
                .Select(r => new { r.ResultId, PatientId = r.Request.PatientId })
                .ToListAsync();

            var latestByPatient = latestResults
                .GroupBy(x => x.PatientId)
                .ToDictionary(g => g.Key, g => (int?)g.First().ResultId);

            var vm = new DoctorAppointmentVm
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentDate = appointment.AppointmentDate,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                Capacity = appointment.Capacity,
                CurrentCount = appointment.CurrentCount,
                Status = appointment.Status,
                Price = appointment.Price,

                Patients = appointment.Bookings
                    .Where(b => b.Patient != null)
                    .Select(b => new DoctorAppointmentPatientVm
                    {
                        PatientId = b.Patient.UserId,
                        FullName = b.Patient.FullName,
                        Age = b.Patient.Age,
                        Phone = b.Patient.Phone ?? "",
                        LatestRadiologyResultId = latestByPatient.TryGetValue(b.Patient.UserId, out var rid) ? rid : null
                    })
                    .ToList()
            };

            return View(vm);
        }

        // ===================== ✅ CREATE PRESCRIPTION (FIXED) =====================

        [HttpGet]
        public IActionResult CreatePrescription(int patientId)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            return View(new CreatePrescriptionVm
            {
                PatientId = patientId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePrescription(CreatePrescriptionVm vm)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            if (!ModelState.IsValid)
                return View(vm);

            var doctorId = CurrentUserId();

            // ✅ تأكد إن المريض موجود
            var patientExists = await _context.Patients.AnyAsync(p => p.UserId == vm.PatientId);
            if (!patientExists)
            {
                ModelState.AddModelError("", "Patient not found.");
                return View(vm);
            }

            // ✅ 1) احفظ الروشتة
            var prescription = new Prescription
            {
                PatientId = vm.PatientId,
                DoctorId = doctorId,
                Diagnosis = vm.Diagnosis.Trim(),
                Treatment = vm.Treatment.Trim(),
                RadiologyRequested = vm.RadiologyRequested,
                CreatedAt = DateTime.UtcNow
            };

            _context.Prescriptions.Add(prescription);
            await _context.SaveChangesAsync(); // ✅ عشان PrescriptionId يتولد

            // ✅ 2) احفظ MedicalRecord مربوط بالروشتة
            var record = new MedicalRecord
            {
                PatientId = vm.PatientId,
                PrescriptionId = prescription.PrescriptionId,
                Description = "Prescription created",
                RecordDate = DateTime.UtcNow
            };

            _context.MedicalRecords.Add(record);
            await _context.SaveChangesAsync();

            TempData["ToastSuccess"] = "✅ Prescription saved.";

            return RedirectToAction(nameof(PatientFile), new { patientId = vm.PatientId });
        }

        // ===================== MEDICAL RECORDS =====================

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

            return RedirectToAction(nameof(MedicalRecords));
        }

        // ===================== PATIENT FILE =====================

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

        // ===================== EDIT DOCTOR PROFILE =====================

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var currentId = CurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            var doctorId = isAdmin ? (id ?? currentId) : currentId;
            if (doctorId <= 0) return RedirectToAction("Login", "Account");

            var doctor = await _context.Set<Doctor>().FirstOrDefaultAsync(d => d.UserId == doctorId);
            if (doctor == null) return NotFound();

            var vm = new DoctorEditVm
            {
                DoctorId = doctor.UserId,
                FullName = doctor.FullName,
                Specialization = doctor.Specialization,
                YearsOfExperience = doctor.YearsOfExperience,
                DepartmentName = doctor.DepartmentName,
                PhoneNumber = doctor.PhoneNumber
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(DoctorEditVm vm)
        {
            var guard = RequireDoctor();
            if (guard != null) return guard;

            var currentId = CurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && vm.DoctorId != currentId)
                return RedirectToAction("AccessDenied", "Account");

            if (!ModelState.IsValid)
                return View(vm);

            var doctor = await _context.Set<Doctor>().FirstOrDefaultAsync(d => d.UserId == vm.DoctorId);
            if (doctor == null) return NotFound();

            doctor.FullName = vm.FullName.Trim();
            doctor.Specialization = vm.Specialization.Trim();
            doctor.YearsOfExperience = vm.YearsOfExperience;
            doctor.DepartmentName = vm.DepartmentName.Trim();
            doctor.PhoneNumber = vm.PhoneNumber.Trim();

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Dashboard));
        }
    }
}
