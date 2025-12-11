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

        public async Task<IActionResult> Dashboard(int? doctorId)
        {
            // ممكن تجيبي ال DoctorId من ال User الحالي
            var id = doctorId ?? 1; // TODO: replace

            var doctor = await _context.Doctors
                .Include(d => d.Staff)
                .Include(d => d.Feedbacks)
                .Include(d => d.Appointments)
                    .ThenInclude(a => a.Bookings)
                    .ThenInclude(b => b.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(d => d.DoctorId == id);

            if (doctor == null) return NotFound();

            var vm = new DoctorProfileVm
            {
                DoctorId = doctor.DoctorId,
                FullName = doctor.Staff.EmployeeName,
                Specialization = doctor.Specialization,
                YearsOfExperience = doctor.YearsOfExperience,
                DepartmentName = doctor.Staff.DepartmentName,
                PhoneNumber = doctor.Staff.PhoneNumber,
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

        public async Task<IActionResult> TodayAppointments(int? doctorId)
        {
            var id = doctorId ?? 1; // TODO

            var today = DateTime.Today;

            var appointments = await _context.Appointments
                .Include(a => a.Bookings).ThenInclude(b => b.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == id && a.AppointmentDate.Date == today)
                .ToListAsync();

            var vm = appointments.Select(a => new AppointmentSummaryVm
            {
                AppointmentId = a.AppointmentId,
                AppointmentDate = a.AppointmentDate,
                StartTime = a.StartTime,
                DoctorName = "", // هو نفسه
                PatientName = a.Bookings.FirstOrDefault()?.Patient.User.FullName ?? "-",
                Status = a.Status
            }).ToList();

            return View(vm);
        }

        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.Staff)
                .Include(a => a.Bookings).ThenInclude(b => b.Patient).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appointment == null) return NotFound();

            var patient = appointment.Bookings.FirstOrDefault()?.Patient;

            PatientProfileVm? patientVm = null;

            if (patient != null)
            {
                patientVm = new PatientProfileVm
                {
                    PatientId = patient.PatientId,
                    FullName = patient.User.FullName,
                    Age = patient.Age,
                    Gender = patient.Gender,
                    Address = patient.Address,
                    MedicalHistorySummary = patient.MedicalHistorySummary
                };
            }

            ViewBag.Appointment = appointment;
            return View(patientVm); // View فيها معلومات الاثنين
        }

        public async Task<IActionResult> PatientFile(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .Include(p => p.MedicalRecords).ThenInclude(r => r.RelatedPrescription)
                .Include(p => p.MedicalRecords).ThenInclude(r => r.RadiologyResult).ThenInclude(rr => rr.Request)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null) return NotFound();

            var vm = new PatientProfileVm
            {
                PatientId = patient.PatientId,
                FullName = patient.User.FullName,
                Age = patient.Age,
                Gender = patient.Gender,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary,
                Records = patient.MedicalRecords.OrderByDescending(r => r.RecordDate)
                    .Select(r => new PatientProfileRecordVm
                    {
                        RecordId = r.RecordId,
                        RecordDate = r.RecordDate,
                        Description = r.Description,
                        Diagnosis = r.RelatedPrescription?.Diagnosis,
                        Treatment = r.RelatedPrescription?.Treatment,
                        RadiologyTestName = r.RadiologyResult?.Request.TestName,
                        RadiologyStatus = r.RadiologyResult?.Status
                    }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult CreatePrescription(int patientId, int doctorId)
        {
            var vm = new PrescriptionFormVm
            {
                PatientId = patientId,
                DoctorId = doctorId
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePrescription(PrescriptionFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var prescription = new Prescription
            {
                PatientId = vm.PatientId,
                DoctorId = vm.DoctorId,
                Diagnosis = vm.Diagnosis,
                Treatment = vm.Treatment,
                RadiologyRequested = vm.RadiologyRequested,
                CreatedAt = DateTime.Now
            };

            _context.Prescriptions.Add(prescription);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(PatientFile), new { id = vm.PatientId });
        }

        [HttpGet]
        public IActionResult CreateAppointment(int doctorId)
        {
            var vm = new DoctorAppointmentVm
            {
                AppointmentDate = DateTime.Today.AddDays(1),
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(9, 30, 0),
                Capacity = 10
            };
            ViewBag.DoctorId = doctorId;
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppointment(int doctorId, DoctorAppointmentVm vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.DoctorId = doctorId;
                return View(vm);
            }

            var appointment = new Appointment
            {
                DoctorId = doctorId,
                AppointmentDate = vm.AppointmentDate,
                StartTime = vm.StartTime,
                EndTime = vm.EndTime,
                Capacity = vm.Capacity,
                CurrentCount = 0,
                Status = "Available",
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Dashboard), new { doctorId });
        }
    }

    public class PrescriptionFormVm
    {
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public string Diagnosis { get; set; } = null!;
        public string Treatment { get; set; } = null!;
        public bool RadiologyRequested { get; set; }
    }
}
