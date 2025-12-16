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

        // =================== AUTH ===================
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

        private IActionResult? RequireReceptionOrAdmin()
        {
            var userId = CurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var type = CurrentUserType();
            if (type != UserType.Receptionist && type != UserType.Admin)
                return RedirectToAction("AccessDenied", "Account");

            return null;
        }

        // =================== DASHBOARD ===================
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var today = DateTime.Today;

            var todaysAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings).ThenInclude(b => b.Patient)
                .Where(a => a.AppointmentDate.Date == today)
                .ToListAsync();

            var recentPatients = await _context.Patients
                .OrderByDescending(p => p.CreatedAt)
                .Take(6)
                .ToListAsync();

            // ✅ هنحط RadiologyRequestsCount في ViewBag عشان Vm بتاعك مفيهوش الخاصية دي
            ViewBag.RadiologyRequestsCount = await _context.RadiologyRequests.CountAsync();

            var vm = new ReceptionDashboardVm
            {
                ReceptionistName = "Receptionist",
                TodaysAppointmentsCount = todaysAppointments.Count,

                TodaysAppointments = todaysAppointments.Select(a => new AppointmentSummaryVm
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    DoctorName = a.Doctor.FullName,
                    PatientName = a.Bookings.Select(b => b.Patient.FullName).FirstOrDefault() ?? "-",
                    Status = a.Status
                }).ToList(),

                RecentPatients = recentPatients.Select(p => new PatientSummaryVm
                {
                    PatientId = p.UserId,
                    FullName = p.FullName,
                    Age = p.Age,
                    Phone = p.Phone!,
                    LastVisit = _context.MedicalRecords
                        .Where(r => r.PatientId == p.UserId)
                        .OrderByDescending(r => r.RecordDate)
                        .Select(r => (DateTime?)r.RecordDate)
                        .FirstOrDefault()
                }).ToList()
            };

            return View(vm);
        }

        // =================== APPOINTMENTS LIST ===================
        [HttpGet]
        public async Task<IActionResult> Appointments()
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var list = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings).ThenInclude(b => b.Patient)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .Select(a => new AppointmentSummaryVm
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    DoctorName = a.Doctor.FullName,
                    PatientName = a.Bookings.Select(b => b.Patient.FullName).FirstOrDefault() ?? "-",
                    Status = a.Status
                })
                .ToListAsync();

            return View(list); // Views/Reception/Appointments.cshtml
        }

        // =================== ADD NEW APPOINTMENT ===================
        [HttpGet]
        public async Task<IActionResult> AddNewAppointment()
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            ViewBag.Doctors = await _context.Doctors
                .OrderBy(d => d.FullName)
                .Select(d => new { d.UserId, d.FullName })
                .ToListAsync();

            ViewBag.Patients = await _context.Patients
                .OrderBy(p => p.FullName)
                .Select(p => new { p.UserId, p.FullName })
                .ToListAsync();

            return View(); // Views/Reception/AddNewAppointment.cshtml
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNewAppointment(int doctorId, int? patientId, DateTime appointmentDate, TimeSpan startTime)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            if (doctorId <= 0)
            {
                TempData["Msg"] = "Please select a doctor.";
                return RedirectToAction(nameof(AddNewAppointment));
            }

            var appt = new Appointment
            {
                DoctorId = doctorId,
                AppointmentDate = appointmentDate.Date,
                StartTime = startTime,
                Status = (patientId.HasValue && patientId.Value > 0) ? "Scheduled" : "Available"
            };

            _context.Appointments.Add(appt);
            await _context.SaveChangesAsync();

            if (patientId.HasValue && patientId.Value > 0)
            {
                var booking = new AppointmentBooking
                {
                    AppointmentId = appt.AppointmentId,
                    PatientId = patientId.Value
                };

                _context.Set<AppointmentBooking>().Add(booking);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Appointments));
        }

        // =================== EDIT APPOINTMENT (Stub) ===================
        [HttpGet]
        public async Task<IActionResult> EditAppointment(int id)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var appt = await _context.Appointments.FindAsync(id);
            if (appt == null) return NotFound();

            TempData["Msg"] = "Edit appointment form not implemented yet.";
            return RedirectToAction(nameof(Appointments));
        }

        // =================== DELETE APPOINTMENT ===================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAppointment(int id)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var appt = await _context.Appointments.FindAsync(id);
            if (appt == null) return NotFound();

            var bookings = await _context.Set<AppointmentBooking>()
                .Where(b => b.AppointmentId == id)
                .ToListAsync();

            if (bookings.Any())
                _context.RemoveRange(bookings);

            _context.Appointments.Remove(appt);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Appointments));
        }

        // =================== CREATE PATIENT ===================
        [HttpGet]
        public IActionResult CreatePatient()
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            return View("PatientForm", new PatientFormVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePatient(PatientFormVm model)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            if (!ModelState.IsValid)
                return View("PatientForm", model);

            var patient = new Patient
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Username = $"patient_{Guid.NewGuid():N}".Substring(0, 12),
                PasswordHash = Guid.NewGuid().ToString(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,

                Gender = model.Gender,
                DateOfBirth = model.DateOfBirth,
                Address = model.Address,
                MedicalHistorySummary = model.MedicalHistorySummary,
                Age = CalculateAge(model.DateOfBirth)
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Patients));
        }

        // =================== EDIT PATIENT ===================
        [HttpGet]
        public async Task<IActionResult> EditPatient(int id)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            var vm = new PatientFormVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Email = patient.Email,
                Phone = patient.Phone!,
                Gender = patient.Gender,
                DateOfBirth = patient.DateOfBirth,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary
            };

            return View("PatientForm", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPatient(PatientFormVm model)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            if (!ModelState.IsValid)
                return View("PatientForm", model);

            var patient = await _context.Patients.FindAsync(model.PatientId);
            if (patient == null) return NotFound();

            patient.FullName = model.FullName;
            patient.Email = model.Email;
            patient.Phone = model.Phone;
            patient.Gender = model.Gender;
            patient.DateOfBirth = model.DateOfBirth;
            patient.Address = model.Address;
            patient.MedicalHistorySummary = model.MedicalHistorySummary;
            patient.Age = CalculateAge(model.DateOfBirth);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Patients));
        }

        // =================== DELETE PATIENT ===================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatient(int id)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Patients));
        }

        // =================== PATIENTS LIST ===================
        [HttpGet]
        public async Task<IActionResult> Patients(string? searchTerm, int page = 1)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            const int pageSize = 10;
            page = page < 1 ? 1 : page;

            var query = _context.Patients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                query = query.Where(p =>
                    p.FullName.Contains(searchTerm) ||
                    (p.Phone != null && p.Phone.Contains(searchTerm)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var patients = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PatientSummaryVm
                {
                    PatientId = p.UserId,
                    FullName = p.FullName,
                    Age = p.Age,
                    Phone = p.Phone!,
                    LastVisit = _context.MedicalRecords
                        .Where(r => r.PatientId == p.UserId)
                        .OrderByDescending(r => r.RecordDate)
                        .Select(r => (DateTime?)r.RecordDate)
                        .FirstOrDefault()
                })
                .ToListAsync();

            var vm = new PatientListVm
            {
                Patients = patients,
                SearchTerm = searchTerm ?? "",
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(vm);
        }

        // =================== PATIENT DETAILS ===================
        [HttpGet]
        public async Task<IActionResult> PatientDetails(int id)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.UserId == id);
            if (patient == null) return NotFound();

            ViewBag.PatientHeader = new PatientHeaderVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Age = patient.Age,
                DateOfBirth = patient.DateOfBirth,
                Phone = patient.Phone ?? "-",
                Address = patient.Address ?? "-",
                Allergies = patient.MedicalHistorySummary
            };

            var records = await _context.MedicalRecords
                .Where(r => r.PatientId == id)
                .OrderByDescending(r => r.RecordDate)
                .Select(r => new PatientProfileRecordVm
                {
                    RecordId = r.RecordId,
                    RecordDate = r.RecordDate,
                    Description = r.Description,
                    Diagnosis = null,
                    Treatment = null,
                    RadiologyTestName = null,
                    RadiologyStatus = null
                })
                .ToListAsync();

            var vm = new PatientProfileVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Age = patient.Age,
                Gender = patient.Gender ?? "-",
                Address = patient.Address ?? "-",
                MedicalHistorySummary = patient.MedicalHistorySummary,
                Records = records
            };

            return View(vm);
        }

        // =================== RADIOLOGY REQUESTS (✅ MATCH YOUR VM) ===================
        [HttpGet]
        public async Task<IActionResult> RadiologyRequests(string? searchTerm, int page = 1)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            const int pageSize = 10;
            page = page < 1 ? 1 : page;

            var query = _context.RadiologyRequests
                .Include(r => r.Patient)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                query = query.Where(r =>
                    r.Patient.FullName.Contains(searchTerm) ||
                    (r.Patient.Phone != null && r.Patient.Phone.Contains(searchTerm)) ||
                    r.TestName.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var requests = await query
                .OrderByDescending(r => r.RequestDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new RadiologyRequestItemVm
                {
                    RequestId = r.RequestId,
                    PatientName = r.Patient.FullName,
                    Age = r.Patient.Age,
                    Phone = r.Patient.Phone ?? "-",
                    TestName = r.TestName,
                    RequestDate = r.RequestDate,
                    Status = r.Status ?? "Pending"
                })
                .ToListAsync();

            var vm = new RadiologyRequestListVm
            {
                Requests = requests,
                SearchTerm = searchTerm ?? "",
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(vm); // Views/Reception/RadiologyRequests.cshtml
        }

        // =================== Radiology Request Details ===================
        [HttpGet]
        public async Task<IActionResult> RadiologyRequestDetails(int id)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var request = await _context.RadiologyRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .Include(r => r.Result)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            if (request.Result == null)
                request.Result = new RadiologyResult();

            return View(request); // Views/Reception/RadiologyRequestDetails.cshtml
        }

        // =================== HELPERS ===================
        private int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return age;
        }
    }
}