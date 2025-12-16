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

            var vm = await GetPatientVM(userId);
            if (vm == null) return NotFound();

            return View(vm);
        }

        public async Task<IActionResult> Prescriptions(int? id)
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

        public async Task<IActionResult> MedicalRecords(int? id)
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
