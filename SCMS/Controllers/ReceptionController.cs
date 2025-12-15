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

        // =================== Dashboard ===================
        public async Task<IActionResult> Dashboard()
        {
            var userId = CurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Receptionist && CurrentUserType() != UserType.Admin)
                return Forbid();

            var today = DateTime.Today;

            var todaysAppointments = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings)
                    .ThenInclude(b => b.Patient)
                .Where(a => a.AppointmentDate.Date == today)
                .ToListAsync();

            var recentPatients = await _context.Set<Patient>()
                .OrderByDescending(p => p.CreatedAt)
                .Take(6)
                .ToListAsync();

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
                    PatientName = a.Bookings.FirstOrDefault()?.Patient.FullName ?? "-",
                    Status = a.Status
                }).ToList(),
                RecentPatients = recentPatients.Select(p => new PatientSummaryVm
                {
                    PatientId = p.UserId,
                    FullName = p.FullName,
                    Age = p.Age,
                    Phone = p.Phone,
                    LastVisit = _context.MedicalRecords
                        .Where(r => r.PatientId == p.UserId)
                        .OrderByDescending(r => r.RecordDate)
                        .Select(r => (DateTime?)r.RecordDate)
                        .FirstOrDefault()
                }).ToList()
            };

            return View(vm);
        }

        // =================== Patients List ===================
        public async Task<IActionResult> Patients(int page = 1, string? searchTerm = null)
        {
            const int pageSize = 10;

            var query = _context.Set<Patient>().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.FullName.Contains(searchTerm) || p.Phone.Contains(searchTerm));
            }

            var totalCount = await query.CountAsync();

            var patients = await query
                .OrderBy(p => p.FullName)
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
                    PatientId = p.UserId,
                    FullName = p.FullName,
                    Age = p.Age,
                    Phone = p.Phone,
                    LastVisit = _context.MedicalRecords
                        .Where(r => r.PatientId == p.UserId)
                        .OrderByDescending(r => r.RecordDate)
                        .Select(r => (DateTime?)r.RecordDate)
                        .FirstOrDefault()
                }).ToList()
            };

            return View(vm);
        }

        // =================== Edit / Add Patient ===================
        public async Task<IActionResult> EditPatient(int? id)
        {
            if (id == null)
                return View(new PatientFormVm());

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            var vm = new PatientFormVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Phone = patient.Phone,
                Email = patient.Email,
                Gender = patient.Gender,
                DateOfBirth = patient.DateOfBirth,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePatient(PatientFormVm model)
        {
            if (!ModelState.IsValid)
                return View("EditPatient", model);

            if (model.PatientId.HasValue)
            {
                // Edit existing
                var patient = await _context.Patients.FindAsync(model.PatientId.Value);
                if (patient == null) return NotFound();

                patient.FullName = model.FullName;
                patient.Phone = model.Phone;
                patient.Email = model.Email;
                patient.Gender = model.Gender;
                patient.DateOfBirth = model.DateOfBirth;
                patient.Address = model.Address;
                patient.MedicalHistorySummary = model.MedicalHistorySummary;

                _context.Update(patient);
            }
            else
            {
                // Add new
                var patient = new Patient
                {
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email,
                    Gender = model.Gender,
                    DateOfBirth = model.DateOfBirth,
                    Address = model.Address,
                    MedicalHistorySummary = model.MedicalHistorySummary,
                    CreatedAt = DateTime.Now
                };
                _context.Add(patient);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Patients");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePatient(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();
            return RedirectToAction("Patients");
        }

        // =================== Patient Details ===================
        public async Task<IActionResult> PatientDetails(int id)
        {
            var patient = await _context.Set<Patient>()
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (patient == null) return NotFound();

            var records = await _context.MedicalRecords
                .Include(r => r.RelatedPrescription)
                .Include(r => r.RadiologyResult)
                    .ThenInclude(rr => rr.Request)
                .Where(r => r.PatientId == id)
                .OrderByDescending(r => r.RecordDate)
                .ToListAsync();

            var header = new PatientHeaderVm
            {
                PatientId = patient.UserId,
                FullName = patient.FullName,
                Age = patient.Age,
                DateOfBirth = patient.DateOfBirth,
                Phone = patient.Phone,
                Address = patient.Address,
                Allergies = patient.MedicalHistorySummary
            };

            var profile = new PatientProfileVm
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
                    RadiologyTestName = r.RadiologyResult?.Request.TestName,
                    RadiologyStatus = r.RadiologyResult?.Status
                }).ToList()
            };

            ViewBag.PatientHeader = header;
            return View(profile);
        }

        // =================== Appointments ===================
        public async Task<IActionResult> Appointments()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings).ThenInclude(b => b.Patient)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            var vm = appointments.Select(a => new AppointmentSummaryVm
            {
                AppointmentId = a.AppointmentId,
                AppointmentDate = a.AppointmentDate,
                StartTime = a.StartTime,
                DoctorName = a.Doctor.FullName,
                PatientName = a.Bookings.FirstOrDefault()?.Patient.FullName ?? "-",
                Status = a.Status
            }).ToList();

            return View(vm);
        }

        // =================== Radiology Requests ===================
        public async Task<IActionResult> RadiologyRequests()
        {
            var requests = await _context.RadiologyRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var vm = new RadiologyRequestListVm
            {
                Requests = requests.Select(r => new RadiologyRequestItemVm
                {
                    RequestId = r.RequestId,
                    PatientName = r.Patient.FullName,
                    Age = r.Patient.Age,
                    Phone = r.Patient.Phone,
                    RequestDate = r.RequestDate,
                    TestName = r.TestName,
                    Status = r.Status
                }).ToList()
            };

            return View(vm);
        }

        // =================== Radiology Request Details ===================
        // =================== Radiology Request Details ===================
        public async Task<IActionResult> RadiologyRequestDetails(int id)
        {
            var request = await _context.RadiologyRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .Include(r => r.Result)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            // تأكد من Result ولو null نعمله object فارغ لتجنب NullReference
            if (request.Result == null)
            {
                request.Result = new RadiologyResult();
            }

            return View(request);
        }
    }
}