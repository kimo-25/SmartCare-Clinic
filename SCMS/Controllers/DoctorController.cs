using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;

namespace SCMS.Controllers
{
    public class DoctorController : Controller
    {
        private readonly AppDbContext _context;

        public DoctorController(AppDbContext context)
        {
            _context = context;
        }

        // ------------------------
        // helper: get doctor id from session
        private int? GetDoctorIdFromSession()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrWhiteSpace(userIdStr)) return null;
            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        // ------------------------
        // Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var id = GetDoctorIdFromSession();
            if (id == null) return RedirectToAction("Login", "Account");

            var doctor = await _context.Set<Doctor>()
                .Include(d => d.Feedbacks)
                .Include(d => d.Appointments)
                    .ThenInclude(a => a.Bookings)
                    .ThenInclude(b => b.Patient)
                .FirstOrDefaultAsync(d => d.UserId == id.Value);

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

        // ------------------------
        // 1️⃣ Appointments List
        public async Task<IActionResult> Appointments()
        {
            var id = GetDoctorIdFromSession();
            if (id == null) return RedirectToAction("Login", "Account");

            var doctor = await _context.Set<Doctor>()
                .Include(d => d.Appointments)
                .FirstOrDefaultAsync(d => d.UserId == id.Value);

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

        // ------------------------
        // 2️⃣ Appointment Details
        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == id && a.DoctorId == doctorId.Value);

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

        // ------------------------
        // 3️⃣ Create Appointment
        [HttpGet]
        public IActionResult CreateAppointment()
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAppointment(DoctorAppointmentVm vm)
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
                return View(vm);

            var appointment = new Appointment
            {
                DoctorId = doctorId.Value,
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

        // ------------------------
        // 4️⃣ Create Prescription
        [HttpGet]
        public IActionResult CreatePrescription(int patientId)
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            ViewBag.PatientId = patientId;
            ViewBag.DoctorId = doctorId.Value;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePrescription(Prescription vm)
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
                return View(vm);

            // enforce doctor id from session (prevent tampering)
            vm.DoctorId = doctorId.Value;
            vm.CreatedAt = DateTime.UtcNow;

            _context.Prescriptions.Add(vm);
            await _context.SaveChangesAsync();

            return RedirectToAction("Dashboard");
        }

        // ------------------------
        // 5️⃣ Medical Records - List
        public async Task<IActionResult> MedicalRecords()
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var records = await _context.MedicalRecords
                .Include(r => r.Patient)
                .Include(r => r.RelatedPrescription)
                .Where(r => r.RelatedPrescription == null || r.RelatedPrescription.DoctorId == doctorId.Value)
                .ToListAsync();

            return View(records);
        }

        // ------------------------
        // 6️⃣ Medical Record Details
        public async Task<IActionResult> DetailsMedicalRecord(int id)
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var record = await _context.MedicalRecords
                .Include(r => r.Patient)
                .Include(r => r.RelatedPrescription)
                .Include(r => r.RadiologyResult)
                .FirstOrDefaultAsync(r => r.RecordId == id);

            if (record == null) return NotFound();

            // optional: ensure this record belongs to this doctor if it has prescription
            if (record.RelatedPrescription != null && record.RelatedPrescription.DoctorId != doctorId.Value)
                return Forbid();

            return View(record);
        }

        // ------------------------
        // 7️⃣ Edit Medical Record - GET
        [HttpGet]
        public async Task<IActionResult> EditMedicalRecord(int id)
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            var record = await _context.MedicalRecords
                .Include(r => r.RelatedPrescription)
                .FirstOrDefaultAsync(r => r.RecordId == id);

            if (record == null) return NotFound();

            if (record.RelatedPrescription != null && record.RelatedPrescription.DoctorId != doctorId.Value)
                return Forbid();

            return View(record);
        }

        // ------------------------
        // 8️⃣ Edit Medical Record - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditMedicalRecord(MedicalRecord vm)
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
                return View(vm);

            var record = await _context.MedicalRecords
                .Include(r => r.RelatedPrescription)
                .FirstOrDefaultAsync(r => r.RecordId == vm.RecordId);

            if (record == null) return NotFound();

            if (record.RelatedPrescription != null && record.RelatedPrescription.DoctorId != doctorId.Value)
                return Forbid();

            record.Description = vm.Description;
            record.PrescriptionId = vm.PrescriptionId;
            record.RadiologyResultId = vm.RadiologyResultId;

            await _context.SaveChangesAsync();

            return RedirectToAction("MedicalRecords");
        }

        // ------------------------
        // Patient File
        public async Task<IActionResult> PatientFile(int patientId)
        {
            var doctorId = GetDoctorIdFromSession();
            if (doctorId == null) return RedirectToAction("Login", "Account");

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
