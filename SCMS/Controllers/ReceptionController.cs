using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.BL.BLInterfaces;
using SCMS.Models;
using SCMS.ViewModels;

namespace SCMS.Controllers
{
    public class ReceptionController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IInvoiceService _invoiceService;

        public ReceptionController(AppDbContext context, IInvoiceService invoiceService)
        {
            _context = context;
            _invoiceService = invoiceService;
        }

        // =================== HELPERS ===================
        private int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return age;
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

        private IActionResult? RequireReceptionOrAdmin()
        {
            var userId = CurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var type = CurrentUserType();
            if (type != UserType.Receptionist && type != UserType.Admin)
                return RedirectToAction("AccessDenied", "Account");

            return null;
        }

        // =================== DASHBOARD (NEW) ===================
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var today = DateTime.Today;

            var todaysSlots = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings)
                .Where(a => a.AppointmentDate.Date == today)
                .OrderBy(a => a.StartTime)
                .Select(a => new AppointmentSummaryVm
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    DoctorName = a.Doctor.FullName,
                    Status = a.Status,

                    Price = a.Price,
                    Capacity = a.Capacity,
                    CurrentCount = a.Bookings.Count(b => b.Status == "Booked"),
                    Remaining = Math.Max(0, a.Capacity - a.Bookings.Count(b => b.Status == "Booked"))
                })
                .ToListAsync();

            var recentPatients = await _context.Patients
                .OrderByDescending(p => p.CreatedAt)
                .Take(6)
                .Select(p => new PatientSummaryVm
                {
                    PatientId = p.UserId,
                    FullName = p.FullName,
                    Age = p.Age,
                    Phone = p.Phone ?? "-",
                    LastVisit = _context.MedicalRecords
                        .Where(r => r.PatientId == p.UserId)
                        .OrderByDescending(r => r.RecordDate)
                        .Select(r => (DateTime?)r.RecordDate)
                        .FirstOrDefault()
                })
                .ToListAsync();

            ViewBag.RadiologyRequestsCount = await _context.RadiologyRequests.CountAsync();

            var vm = new ReceptionDashboardVm
            {
                ReceptionistName = "Receptionist",
                TodaysAppointmentsCount = todaysSlots.Count,
                TodaysAppointments = todaysSlots,
                RecentPatients = recentPatients
            };

            return View(vm);
        }

        // ===================== SLOTS LIST =====================
        [HttpGet]
        public async Task<IActionResult> Slots(string? searchTerm, DateTime? date)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var q = _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings)
                .AsQueryable();

            if (date.HasValue)
            {
                var d = date.Value.Date;
                q = q.Where(a => a.AppointmentDate.Date == d);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                q = q.Where(a => a.Doctor.FullName.Contains(searchTerm));
            }

            var slots = await q
                .OrderByDescending(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .Select(a => new ReceptionSlotRowVm
                {
                    AppointmentId = a.AppointmentId,
                    AppointmentDate = a.AppointmentDate,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    DoctorId = a.DoctorId,
                    DoctorName = a.Doctor.FullName,
                    Price = a.Price,
                    Capacity = a.Capacity,
                    BookedCount = a.Bookings.Count(b => b.Status == "Booked"),
                    Status = a.Status
                })
                .ToListAsync();

            return View(new ReceptionSlotsListVm
            {
                SearchTerm = searchTerm,
                Date = date,
                Slots = slots
            });
        }

        // ===================== CREATE SLOT =====================
        [HttpGet]
        public async Task<IActionResult> CreateSlot()
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            ViewBag.Doctors = await _context.Doctors
                .OrderBy(d => d.FullName)
                .Select(d => new { d.UserId, d.FullName })
                .ToListAsync();

            return View(new ReceptionCreateSlotVm
            {
                AppointmentDate = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSlot(ReceptionCreateSlotVm vm, string? returnUrl)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            if (!ModelState.IsValid)
            {
                ViewBag.Doctors = await _context.Doctors
                    .OrderBy(d => d.FullName)
                    .Select(d => new { d.UserId, d.FullName })
                    .ToListAsync();
                return View(vm);
            }

            if (vm.EndTime <= vm.StartTime)
            {
                ModelState.AddModelError("", "EndTime لازم يكون بعد StartTime.");
                ViewBag.Doctors = await _context.Doctors
                    .OrderBy(d => d.FullName)
                    .Select(d => new { d.UserId, d.FullName })
                    .ToListAsync();
                return View(vm);
            }

            var doctorExists = await _context.Doctors.AnyAsync(d => d.UserId == vm.DoctorId);
            if (!doctorExists)
            {
                ModelState.AddModelError("", "Doctor غير موجود.");
                ViewBag.Doctors = await _context.Doctors
                    .OrderBy(d => d.FullName)
                    .Select(d => new { d.UserId, d.FullName })
                    .ToListAsync();
                return View(vm);
            }

            // منع تداخل مواعيد نفس الدكتور
            var overlap = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == vm.DoctorId &&
                a.AppointmentDate.Date == vm.AppointmentDate.Date &&
                a.Status != "Cancelled" &&
                !(vm.EndTime <= a.StartTime || vm.StartTime >= a.EndTime)
            );

            if (overlap)
            {
                ModelState.AddModelError("", "فيه Slot متداخل لنفس الدكتور في نفس الوقت.");
                ViewBag.Doctors = await _context.Doctors
                    .OrderBy(d => d.FullName)
                    .Select(d => new { d.UserId, d.FullName })
                    .ToListAsync();
                return View(vm);
            }

            var appt = new Appointment
            {
                DoctorId = vm.DoctorId,
                CreatedByUserId = CurrentUserId(),
                AppointmentDate = vm.AppointmentDate.Date,
                StartTime = vm.StartTime,
                EndTime = vm.EndTime,
                Capacity = vm.Capacity,
                Price = vm.Price,
                Status = vm.Status,
                CurrentCount = 0
            };

            _context.Appointments.Add(appt);
            await _context.SaveChangesAsync();

            TempData["ToastSuccess"] = "✅ Clinic slot created successfully.";

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var abs))
                    return LocalRedirect(abs.PathAndQuery);
                if (Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Slots));
        }

        // ===================== BOOK PATIENT IN SLOT =====================
        [HttpGet]
        public async Task<IActionResult> BookPatient(int appointmentId)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var appt = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appt == null) return NotFound();

            var bookedCount = appt.Bookings.Count(b => b.Status == "Booked");
            var remaining = Math.Max(0, appt.Capacity - bookedCount);

            if (appt.Status != "Available" || remaining <= 0)
            {
                TempData["ToastSuccess"] = "⚠️ This slot is not available for booking.";
                return RedirectToAction(nameof(Slots));
            }

            ViewBag.AppointmentInfo = new
            {
                appt.AppointmentId,
                appt.Doctor.FullName,
                appt.AppointmentDate,
                appt.StartTime,
                appt.EndTime,
                appt.Price,
                appt.Capacity,
                BookedCount = bookedCount,
                Remaining = remaining
            };

            ViewBag.Patients = await _context.Patients
                .OrderBy(p => p.FullName)
                .Select(p => new { p.UserId, p.FullName, p.Phone })
                .ToListAsync();

            return View(new ReceptionBookPatientVm { AppointmentId = appointmentId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookPatient(ReceptionBookPatientVm vm, string? returnUrl)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            if (!ModelState.IsValid)
                return RedirectToAction(nameof(BookPatient), new { appointmentId = vm.AppointmentId });

            var appt = await _context.Appointments
                .Include(a => a.Bookings)
                .FirstOrDefaultAsync(a => a.AppointmentId == vm.AppointmentId);

            if (appt == null) return NotFound();

            if (appt.Status != "Available")
                return RedirectToAction("AccessDenied", "Account");

            var bookedCount = appt.Bookings.Count(b => b.Status == "Booked");
            var remaining = Math.Max(0, appt.Capacity - bookedCount);

            if (remaining <= 0)
            {
                TempData["ToastSuccess"] = "⚠️ Capacity is full. Cannot book.";
                return RedirectToAction(nameof(Slots));
            }

            var alreadyBooked = await _context.AppointmentBookings.AnyAsync(b =>
                b.AppointmentId == vm.AppointmentId &&
                b.PatientId == vm.PatientId &&
                b.Status == "Booked");

            if (alreadyBooked)
            {
                TempData["ToastSuccess"] = "⚠️ Patient already booked in this slot.";
                return RedirectToAction(nameof(Slots));
            }

            var maxOrder = await _context.AppointmentBookings
                .Where(b => b.AppointmentId == vm.AppointmentId)
                .Select(b => (int?)b.OrderNumber)
                .MaxAsync() ?? 0;

            var booking = new AppointmentBooking
            {
                AppointmentId = vm.AppointmentId,
                PatientId = vm.PatientId,
                OrderNumber = maxOrder + 1,
                Status = "Booked",
                CreatedAt = DateTime.UtcNow
            };

            _context.AppointmentBookings.Add(booking);
            await _context.SaveChangesAsync();

            // ✅ Create invoice for this booking
            _invoiceService.CreateForBooking(booking.BookingId);

            TempData["ToastSuccess"] = "✅ Patient booked successfully.";

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var abs))
                    return LocalRedirect(abs.PathAndQuery);
                if (Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Slots));
        }

        // ===================== CANCEL BOOKING =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId, string? returnUrl)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var booking = await _context.AppointmentBookings
                .Include(b => b.Invoice)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null) return NotFound();

            booking.Status = "Cancelled";

            if (booking.Invoice != null)
                booking.Invoice.Status = "Cancelled";

            await _context.SaveChangesAsync();

            TempData["ToastSuccess"] = "✅ Booking cancelled.";

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var abs))
                    return LocalRedirect(abs.PathAndQuery);
                if (Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Slots));
        }

        // ===================== CANCEL SLOT =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSlot(int appointmentId, string? returnUrl)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var appt = await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId);

            if (appt == null) return NotFound();

            appt.Status = "Cancelled";
            await _context.SaveChangesAsync();

            TempData["ToastSuccess"] = "✅ Slot cancelled.";

            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var abs))
                    return LocalRedirect(abs.PathAndQuery);
                if (Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Slots));
        }

        // ===================== SLOT DETAILS =====================
        [HttpGet]
        public async Task<IActionResult> SlotDetails(int id)
        {
            var guard = RequireReceptionOrAdmin();
            if (guard != null) return guard;

            var appt = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings)
                    .ThenInclude(b => b.Patient)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

            if (appt == null) return NotFound();

            return View(appt);
        }

        // =================== PATIENTS ===================
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
                    Phone = p.Phone ?? "-",
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
                Phone = patient.Phone ?? "",
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

        // =================== RADIOLOGY (LIST + DETAILS) ===================
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

            return View(vm);
        }

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

            return View(request);
        }
    }
}