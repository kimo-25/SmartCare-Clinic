using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;

namespace SCMS.Controllers
{
    public class ReceptionController : Controller
    {
        private readonly AppDbContext _context;

        public ReceptionController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;

            var todaysAppointments = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.Staff)
                .Include(a => a.Bookings).ThenInclude(b => b.Patient).ThenInclude(p => p.User)
                .Where(a => a.AppointmentDate.Date == today)
                .ToListAsync();

            var vm = new ReceptionDashboardVm
            {
                ReceptionistName = User.Identity?.Name ?? "Receptionist",
                TodaysAppointmentsCount = todaysAppointments.Count,
                TodaysAppointments = todaysAppointments.Select(a => new AppointmentSummaryVm
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    DoctorName = a.Doctor.Staff.EmployeeName,
                    PatientName = a.Bookings.FirstOrDefault()?.Patient.User.FullName ?? "-",
                    Status = a.Status
                }).ToList(),
                RecentPatients = await _context.Patients
                    .Include(p => p.User)
                    .OrderByDescending(p => p.PatientId)
                    .Take(6)
                    .Select(p => new PatientSummaryVm
                    {
                        PatientId = p.PatientId,
                        FullName = p.User.FullName,
                        Age = p.Age,
                        Phone = p.User.Phone,
                        LastVisit = p.MedicalRecords
                            .OrderByDescending(r => r.RecordDate)
                            .Select(r => (DateTime?)r.RecordDate)
                            .FirstOrDefault()
                    }).ToListAsync()
            };

            return View(vm);
        }

        public async Task<IActionResult> Patients(int page = 1, string? searchTerm = null)
        {
            const int pageSize = 10;

            var query = _context.Patients
                .Include(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p =>
                    p.User.FullName.Contains(searchTerm) ||
                    p.User.Phone.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();

            var patients = await query
                .OrderBy(p => p.User.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var vm = new PatientListVm
            {
                SearchTerm = searchTerm,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Patients = patients.Select(p => new PatientSummaryVm
                {
                    PatientId = p.PatientId,
                    FullName = p.User.FullName,
                    Age = p.Age,
                    Phone = p.User.Phone,
                    LastVisit = p.MedicalRecords
                        .OrderByDescending(r => r.RecordDate)
                        .Select(r => (DateTime?)r.RecordDate)
                        .FirstOrDefault()
                }).ToList()
            };

            return View(vm);
        }

        public async Task<IActionResult> PatientDetails(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .Include(p => p.MedicalRecords)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null) return NotFound();

            var header = new PatientHeaderVm
            {
                PatientId = patient.PatientId,
                FullName = patient.User.FullName,
                Age = patient.Age,
                DateOfBirth = patient.DateOfBirth,
                Phone = patient.User.Phone,
                Address = patient.Address,
                Allergies = patient.MedicalHistorySummary
            };

            var profile = new PatientProfileVm
            {
                PatientId = patient.PatientId,
                FullName = patient.User.FullName,
                Age = patient.Age,
                Gender = patient.Gender,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary,
                Records = patient.MedicalRecords
                    .OrderByDescending(r => r.RecordDate)
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

            ViewBag.PatientHeader = header;
            return View(profile);
        }

        [HttpGet]
        public IActionResult CreatePatient()
        {
            return View(new PatientFormVm());
        }

        [HttpPost]
        public async Task<IActionResult> CreatePatient(PatientFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = new User
            {
                FullName = vm.FullName,
                Phone = vm.Phone,
                Email = vm.Email,
                Username = vm.Email,
                PasswordHash = "123456", // TODO: password & hash
                Role = "Patient",
                IsActive = true
            };

            var patient = new Patient
            {
                User = user,
                Gender = vm.Gender,
                DateOfBirth = vm.DateOfBirth,
                Address = vm.Address,
                MedicalHistorySummary = vm.MedicalHistorySummary,
                Age = (int)((DateTime.Today - vm.DateOfBirth).TotalDays / 365.25)
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Patients));
        }

        [HttpGet]
        public async Task<IActionResult> EditPatient(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null) return NotFound();

            var vm = new PatientFormVm
            {
                PatientId = patient.PatientId,
                FullName = patient.User.FullName,
                Email = patient.User.Email,
                Phone = patient.User.Phone,
                Gender = patient.Gender,
                DateOfBirth = patient.DateOfBirth,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> EditPatient(PatientFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.PatientId == vm.PatientId);

            if (patient == null) return NotFound();

            patient.User.FullName = vm.FullName;
            patient.User.Email = vm.Email;
            patient.User.Phone = vm.Phone;
            patient.Gender = vm.Gender;
            patient.DateOfBirth = vm.DateOfBirth;
            patient.Address = vm.Address;
            patient.MedicalHistorySummary = vm.MedicalHistorySummary;
            patient.Age = (int)((DateTime.Today - vm.DateOfBirth).TotalDays / 365.25);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Patients));
        }

        public async Task<IActionResult> Appointments()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.Staff)
                .Include(a => a.Bookings).ThenInclude(b => b.Patient).ThenInclude(p => p.User)
                .ToListAsync();

            var vm = appointments.Select(a => new AppointmentSummaryVm
            {
                AppointmentId = a.AppointmentId,
                AppointmentDate = a.AppointmentDate,
                StartTime = a.StartTime,
                DoctorName = a.Doctor.Staff.EmployeeName,
                PatientName = a.Bookings.FirstOrDefault()?.Patient.User.FullName ?? "-",
                Status = a.Status
            }).ToList();

            return View(vm);
        }

        public async Task<IActionResult> RadiologyRequests()
        {
            var requests = await _context.RadiologyRequests
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.Staff)
                .ToListAsync();

            var vm = new RadiologyRequestListVm
            {
                Requests = requests.Select(r => new RadiologyRequestItemVm
                {
                    RequestId = r.RequestId,
                    PatientName = r.Patient.User.FullName,
                    Age = r.Patient.Age,
                    Phone = r.Patient.User.Phone,
                    DayOfRay = r.RequestDate,
                    RayType = r.TestName,
                    Status = r.Status
                }).ToList()
            };

            return View(vm);
        }

        public async Task<IActionResult> RadiologyRequestDetails(int id)
        {
            var request = await _context.RadiologyRequests
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.Staff)
                .Include(r => r.Result)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            return View(request);
        }
    }
}
