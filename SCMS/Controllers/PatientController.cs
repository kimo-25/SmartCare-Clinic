using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels.Patient;

namespace SCMS.Controllers
{
    public class PatientController : Controller
    {
        private readonly AppDbContext _context;

        public PatientController(AppDbContext context)
        {
            _context = context;
        }

        private int CurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            return int.TryParse(userIdStr, out var id) ? id : 0;
        }

        private UserType CurrentUserType()
        {
            var t = HttpContext.Session.GetInt32("UserType");
            return t.HasValue ? (UserType)t.Value : UserType.User;
        }

        private async Task<Patient?> GetPatientByUserId(int userId)
        {
            return await _context.Patients
                .Include(p => p.AppointmentBookings)
                .Include(p => p.Prescriptions)
                .Include(p => p.MedicalRecords)
                .FirstOrDefaultAsync(p => p.UserId == userId);
        }

        private async Task<PatientVM?> GetPatientVM(int userId)
        {
            var patient = await GetPatientByUserId(userId);
            if (patient == null) return null;

            return new PatientVM
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Gender = patient.Gender,
                DateOfBirth = patient.DateOfBirth,
                Age = patient.Age,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary,

                AppointmentsCount = patient.AppointmentBookings.Count,
                PrescriptionsCount = patient.Prescriptions.Count,
                MedicalRecordsCount = patient.MedicalRecords.Count,

                IsProfileIncomplete =
                    patient.Age <= 0 ||
                    patient.Gender == "Unknown" ||
                    string.IsNullOrWhiteSpace(patient.Address) || patient.Address == "N/A" ||
                    string.IsNullOrWhiteSpace(patient.MedicalHistorySummary)
            };
        }

        public async Task<IActionResult> Dashboard(int? id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Patient)
                return RedirectToAction("AccessDenied", "Account");

            var userId = id ?? CurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            var vm = await GetPatientVM(userId);
            if (vm == null) return NotFound();

            return View(vm);
        }

        public async Task<IActionResult> Profile(int? id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Patient)
                return RedirectToAction("AccessDenied", "Account");

            var userId = id ?? CurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            var vm = await GetPatientVM(userId);
            if (vm == null) return NotFound();

            return View(vm);
        }

        public async Task<IActionResult> Appointments(int? id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");
            if (CurrentUserType() != UserType.Patient)
                return RedirectToAction("AccessDenied", "Account");

            var userId = id ?? CurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            var bookings = await _context.AppointmentBookings
                .Include(b => b.Appointment).ThenInclude(a => a.Doctor)
                .Where(b => b.PatientId == userId) // ✅ حجوزات المريض فقط
                .OrderByDescending(b => b.Appointment.AppointmentDate)
                .ThenByDescending(b => b.Appointment.StartTime)
                .Select(b => new PatientAppointmentRowVm
                {
                    BookingId = b.BookingId,
                    AppointmentId = b.AppointmentId,
                    AppointmentDate = b.Appointment.AppointmentDate,
                    StartTime = b.Appointment.StartTime,
                    EndTime = b.Appointment.EndTime,
                    DoctorName = b.Appointment.Doctor.FullName,
                    Price = b.Appointment.Price,
                    BookingStatus = b.Status
                })
                .ToListAsync();

            var vm = await GetPatientVM(userId);
            if (vm == null) return NotFound();

            ViewBag.Bookings = bookings;
            return View(vm);
        }
        public async Task<IActionResult> RadiologyResults(int? id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Patient)
                return RedirectToAction("AccessDenied", "Account");

            var userId = id ?? CurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            // المريض ماينفعش يطلب بيانات مريض تاني
            if (userId != CurrentUserId())
                return RedirectToAction("AccessDenied", "Account");

            var patient = await _context.Patients
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (patient == null) return NotFound();

            var results = await _context.RadiologyResults
                .Include(r => r.Request)
                    .ThenInclude(req => req.Doctor)
                .Include(r => r.Radiologist)
                .Where(r => r.Request.PatientId == userId)
                .OrderByDescending(r => r.ResultDate)
                .Select(r => new PatientRadiologyResultRowVm
                {
                    ResultId = r.ResultId,
                    RequestId = r.RequestId,
                    ResultDate = r.ResultDate,

                    TestName = r.Request.TestName,
                    DoctorName = r.Request.Doctor.FullName,
                    RadiologistName = r.Radiologist.FullName,

                    Report = r.Report,
                    ImagePath = r.ImagePath
                })
                .ToListAsync();

            var vm = new PatientRadiologyResultsVm
            {
                PatientId = patient.UserId,
                PatientName = patient.FullName,
                Results = results
            };

            return View(vm); // Views/Patient/RadiologyResults.cshtml
        }
        public async Task<IActionResult> Prescriptions(int? id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");
            if (CurrentUserType() != UserType.Patient)
                return RedirectToAction("AccessDenied", "Account");

            var userId = id ?? CurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            var list = await _context.Prescriptions
                .Include(p => p.Doctor)
                .Where(p => p.PatientId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PatientPrescriptionRowVm
                {
                    PrescriptionId = p.PrescriptionId,
                    CreatedAt = p.CreatedAt,
                    DoctorName = p.Doctor.FullName,
                    Diagnosis = p.Diagnosis,
                    Treatment = p.Treatment,
                    RadiologyRequested = p.RadiologyRequested
                })
                .ToListAsync();

            var vm = await GetPatientVM(userId);
            if (vm == null) return NotFound();

            ViewBag.PrescriptionsList = list;
            return View(vm);
        }

        public async Task<IActionResult> MedicalRecords(int? id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");
            if (CurrentUserType() != UserType.Patient)
                return RedirectToAction("AccessDenied", "Account");

            var userId = id ?? CurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null) return NotFound();

            // 1) Bookings (للفواتير)
            var bookings = await _context.AppointmentBookings
                .Include(b => b.Appointment).ThenInclude(a => a.Doctor)
                .Include(b => b.Invoice)
                .Where(b => b.PatientId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            // 2) Prescriptions
            var prescriptions = await _context.Prescriptions
                .Include(p => p.Doctor)
                .Where(p => p.PatientId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PatientPrescriptionRowVm
                {
                    PrescriptionId = p.PrescriptionId,
                    CreatedAt = p.CreatedAt,
                    DoctorName = p.Doctor.FullName,
                    Diagnosis = p.Diagnosis,
                    Treatment = p.Treatment,
                    RadiologyRequested = p.RadiologyRequested
                })
                .ToListAsync();

            // 3) Radiology Results
            var results = await _context.RadiologyResults
                .Include(r => r.Request).ThenInclude(req => req.Doctor)
                .Where(r => r.Request.PatientId == userId)
                .OrderByDescending(r => r.ResultDate)
                .Select(r => new PatientRadiologyRowVm
                {
                    ResultId = r.ResultId,
                    ResultDate = r.ResultDate,
                    DoctorName = r.Request.Doctor.FullName,
                    TestName = r.Request.TestName,
                    Status = r.Status,
                    ImagePath = r.ImagePath,
                    Report = r.Report
                })
                .ToListAsync();

            // 4) Build invoices breakdown (كشف + 500 لكل أشعة Completed)
            // الربط: نفس Patient + نفس Doctor + أقرب Booking (حسب CreatedAt)
            var invoicesVm = new List<PatientInvoiceVm>();

            foreach (var b in bookings.Where(x => x.Invoice != null))
            {
                var doctorId = b.Appointment.DoctorId;

                // كل الأشعات المكتملة الخاصة بنفس الدكتور + المريض
                // ونحسب اللي تاريخها بعد الحجز (أو في نفس اليوم) — ده أفضل “ربط منطقي” بدون FK
                var completedRaysCount = await _context.RadiologyResults
                    .Include(r => r.Request)
                    .Where(r =>
                        r.Status == "Completed" &&
                        r.Request.PatientId == userId &&
                        r.Request.DoctorId == doctorId &&
                        r.ResultDate >= b.CreatedAt) // ✅ بعد الكشف/الحجز
                    .CountAsync();

                var lines = new List<PatientInvoiceLineVm>
        {
            new PatientInvoiceLineVm
            {
                Title = $"Consultation - {b.Appointment.Doctor.FullName}",
                Amount = b.Appointment.Price
            }
        };

                if (completedRaysCount > 0)
                {
                    lines.Add(new PatientInvoiceLineVm
                    {
                        Title = $"Radiology fee (500 x {completedRaysCount})",
                        Amount = 500 * completedRaysCount
                    });
                }

                invoicesVm.Add(new PatientInvoiceVm
                {
                    InvoiceId = b.Invoice!.InvoiceId,
                    BookingId = b.BookingId,
                    CreatedAt = b.Invoice.CreatedAt,
                    DoctorName = b.Appointment.Doctor.FullName,
                    Lines = lines
                });
            }

            var fileVm = new PatientMedicalFileVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,

                Appointments = bookings.Select(b => new PatientAppointmentRowVm
                {
                    BookingId = b.BookingId,
                    AppointmentId = b.AppointmentId,
                    AppointmentDate = b.Appointment.AppointmentDate,
                    StartTime = b.Appointment.StartTime,
                    EndTime = b.Appointment.EndTime,
                    DoctorName = b.Appointment.Doctor.FullName,
                    Price = b.Appointment.Price,
                    BookingStatus = b.Status
                }).ToList(),

                Prescriptions = prescriptions,
                RadiologyResults = results,
                Invoices = invoicesVm
            };

            return View("MedicalFile", fileVm); // ✅ View جديدة
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Patient)
                return RedirectToAction("AccessDenied", "Account");

            var userId = id ?? CurrentUserId();
            if (userId <= 0) return RedirectToAction("Login", "Account");

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == userId);
            if (patient == null) return NotFound();

            var vm = new PatientEditVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Gender = patient.Gender,
                DateOfBirth = patient.DateOfBirth,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PatientEditVm vm)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Patient)
                return RedirectToAction("AccessDenied", "Account");

            var sessionUserId = CurrentUserId();
            if (sessionUserId <= 0) return RedirectToAction("Login", "Account");

            if (vm.PatientId != sessionUserId)
                return RedirectToAction("AccessDenied", "Account");

            if (!ModelState.IsValid)
                return View(vm);

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == vm.PatientId);
            if (patient == null) return NotFound();

            patient.FullName = vm.FullName?.Trim() ?? patient.FullName;
            patient.Gender = vm.Gender?.Trim() ?? patient.Gender;
            patient.DateOfBirth = vm.DateOfBirth;
            patient.Address = vm.Address?.Trim() ?? patient.Address;
            patient.MedicalHistorySummary = string.IsNullOrWhiteSpace(vm.MedicalHistorySummary)
                ? null
                : vm.MedicalHistorySummary.Trim();

            patient.Age = CalculateAge(patient.DateOfBirth);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Dashboard), new { id = patient.UserId });
        }

        private static int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            if (age < 0) age = 0;
            return age;
        }
    }
}
