using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;
using System.Security.Claims;

namespace SCMS.Controllers
{
    public class RadiologyController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public RadiologyController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
            if (User.IsInRole("Receptionist")) return UserType.Receptionist;
            if (User.IsInRole("Radiologist")) return UserType.Radiologist;

            return UserType.User;
        }

        private IActionResult? RequireLogin()
        {
            return CurrentUserId() == 0 ? RedirectToAction("Login", "Account") : null;
        }

        private IActionResult? RequireRadiologyStaff()
        {
            var login = RequireLogin();
            if (login != null) return login;

            var type = CurrentUserType();
            if (type != UserType.Radiologist && type != UserType.Admin && type != UserType.Receptionist)
                return RedirectToAction("AccessDenied", "Account");

            return null;
        }

        private IActionResult? RequireRadiologyCreators()
        {
            var login = RequireLogin();
            if (login != null) return login;

            var type = CurrentUserType();
            if (type != UserType.Radiologist && type != UserType.Admin && type != UserType.Receptionist && type != UserType.Doctor)
                return RedirectToAction("AccessDenied", "Account");

            return null;
        }

        private IActionResult? SafeReturn(string? returnUrl)
        {
            if (string.IsNullOrWhiteSpace(returnUrl)) return null;

            if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var abs))
                return LocalRedirect(abs.PathAndQuery);

            if (Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return null;
        }

        // ========================= Requests List =========================
        public async Task<IActionResult> Requests(string? status)
        {
            var guard = RequireRadiologyStaff();
            if (guard != null) return guard;

            var q = _context.RadiologyRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .Include(r => r.Radiologist)
                .Include(r => r.Result)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    q = q.Where(r => r.Result == null);
                else if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                    q = q.Where(r => r.Result != null);
            }

            var requests = await q
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var vm = new RadiologyRequestListVm
            {
                Requests = requests.Select(r => new RadiologyRequestItemVm
                {
                    RequestId = r.RequestId,
                    ResultId = r.Result != null ? r.Result.ResultId : (int?)null,
                    PatientName = r.Patient.FullName,
                    Age = r.Patient.Age,
                    Phone = r.Patient.Phone ?? "",
                    RequestDate = r.RequestDate,
                    TestName = r.TestName,
                    Status = (r.Result == null) ? "Pending" : "Completed"
                }).ToList()
            };

            return View(vm);
        }

        // ========================= Create Request =========================
        [HttpGet]
        public IActionResult CreateRequest(int patientId, int doctorId)
        {
            var guard = RequireRadiologyCreators();
            if (guard != null) return guard;

            if (CurrentUserType() == UserType.Doctor)
                doctorId = CurrentUserId();

            return View(new RadiologyRequestFormVm
            {
                PatientId = patientId,
                DoctorId = doctorId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRequest(RadiologyRequestFormVm vm, string? returnUrl)
        {
            var guard = RequireRadiologyCreators();
            if (guard != null) return guard;

            if (CurrentUserType() == UserType.Doctor)
                vm.DoctorId = CurrentUserId();

            if (!ModelState.IsValid)
                return View(vm);

            var patientExists = await _context.Patients.AnyAsync(p => p.UserId == vm.PatientId);
            var doctorExists = await _context.Doctors.AnyAsync(d => d.UserId == vm.DoctorId);

            if (!patientExists || !doctorExists)
            {
                ModelState.AddModelError("", "Patient أو Doctor غير موجود.");
                return View(vm);
            }

            var request = new RadiologyRequest
            {
                PatientId = vm.PatientId,
                DoctorId = vm.DoctorId,
                PrescriptionId = vm.PrescriptionId,
                TestName = vm.TestName,
                ClinicalNotes = vm.ClinicalNotes,
                Status = "Pending",
                RequestDate = DateTime.UtcNow
            };

            _context.RadiologyRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["ToastSuccess"] = "✅ Radiology request saved successfully.";

            var back = SafeReturn(returnUrl);
            if (back != null) return back;

            // doctor مايروحش Requests لأنها غالبًا ممنوعة عليه في السيستم عندك
            if (CurrentUserType() == UserType.Doctor)
                return RedirectToAction("Dashboard", "Doctor");

            return RedirectToAction(nameof(Requests));
        }

        // ========================= Request Details =========================
        public async Task<IActionResult> RequestDetails(int id)
        {
            var login = RequireLogin();
            if (login != null) return login;

            var type = CurrentUserType();
            var userId = CurrentUserId();

            var request = await _context.RadiologyRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .Include(r => r.Radiologist)
                .Include(r => r.Result)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            if (type == UserType.Patient && request.PatientId != userId)
                return RedirectToAction("AccessDenied", "Account");

            if (type == UserType.Doctor && request.DoctorId != userId)
                return RedirectToAction("AccessDenied", "Account");

            if (type == UserType.Radiologist && request.RadiologistId.HasValue && request.RadiologistId.Value != userId)
                return RedirectToAction("AccessDenied", "Account");

            return View(request);
        }

        // ========================= Create Result =========================
        [HttpGet]
        public IActionResult CreateResult(int requestId)
        {
            var login = RequireLogin();
            if (login != null) return login;

            if (CurrentUserType() != UserType.Radiologist)
                return RedirectToAction("AccessDenied", "Account");

            return View(new RadiologyResultFormVm
            {
                RequestId = requestId,
                RadiologistId = CurrentUserId(),
                Status = "Completed"
            });
        }

        // ✅ Helper: زوّد 500 على Invoice لما الأشعة تبقى Completed
        private async Task ApplyRadiologyFeeAsync(int requestId, double fee = 500)
        {
            var req = await _context.RadiologyRequests
                .Include(r => r.Result)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (req == null) return;
            if (req.Result == null) return;
            if (!string.Equals(req.Result.Status, "Completed", StringComparison.OrdinalIgnoreCase)) return;

            // هات أحدث Booking للمريض عند نفس الدكتور (Booked)
            var booking = await _context.AppointmentBookings
                .Include(b => b.Invoice)
                .Include(b => b.Appointment)
                .Where(b =>
                    b.PatientId == req.PatientId &&
                    b.Status == "Booked" &&
                    b.Appointment.DoctorId == req.DoctorId)
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefaultAsync();

            if (booking == null) return;

            // لو مفيش Invoice اعمل واحدة بسعر الكشف
            var invoice = booking.Invoice;
            if (invoice == null)
            {
                invoice = new Invoice
                {
                    BookingId = booking.BookingId,
                    TotalAmount = booking.Appointment.Price,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Not Billed yet"
                };
                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();
            }

            // زوّد رسوم الأشعة
            invoice.TotalAmount += fee;

            // لو مدفوعة خليك واضح إنها اتعدلت
            if (string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                invoice.Status = "Paid (Adjusted)";
            else if (string.Equals(invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                invoice.Status = "Cancelled"; // ما نغيرهاش
            else
                invoice.Status = "Not Billed yet";

            await _context.SaveChangesAsync();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateResult(RadiologyResultFormVm vm, string? returnUrl)
        {
            var login = RequireLogin();
            if (login != null) return login;

            var type = CurrentUserType();
            if (type != UserType.Radiologist && type != UserType.Admin)
                return RedirectToAction("AccessDenied", "Account");

            vm.RadiologistId = CurrentUserId();

            if (!ModelState.IsValid)
                return View(vm);

            var req = await _context.RadiologyRequests
                .Include(r => r.Result)
                .FirstOrDefaultAsync(r => r.RequestId == vm.RequestId);

            if (req == null) return NotFound();

            if (req.Result != null)
            {
                ModelState.AddModelError("", "هذا الطلب له نتيجة بالفعل.");
                return View(vm);
            }

            if (vm.RayFile == null || vm.RayFile.Length == 0)
            {
                ModelState.AddModelError("", "من فضلك ارفع ملف الأشعة.");
                return View(vm);
            }

            var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "radiology");
            Directory.CreateDirectory(uploadsRoot);

            var ext = Path.GetExtension(vm.RayFile.FileName);
            var safeExt = string.IsNullOrWhiteSpace(ext) ? ".bin" : ext.ToLowerInvariant();

            var fileName = $"ray_req{vm.RequestId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{safeExt}";
            var fullPath = Path.Combine(uploadsRoot, fileName);

            using (var fs = new FileStream(fullPath, FileMode.Create))
            {
                await vm.RayFile.CopyToAsync(fs);
            }

            var dbPath = $"/uploads/radiology/{fileName}";

            var result = new RadiologyResult
            {
                RequestId = vm.RequestId,
                RadiologistId = vm.RadiologistId,
                ImagePath = dbPath,
                Report = vm.Report,
                Status = string.IsNullOrWhiteSpace(vm.Status) ? "Completed" : vm.Status,
                ResultDate = DateTime.UtcNow
            };

            _context.RadiologyResults.Add(result);

            req.Status = "Completed";
            req.RadiologistId = vm.RadiologistId;

            await _context.SaveChangesAsync();

            // ✅ أهم جزء: عدّل الفاتورة وزوّد 500 لكل Result Completed
            await ApplyRadiologyFeeAsync(vm.RequestId, 500);

            TempData["ToastSuccess"] = "✅ Result saved. Invoice updated (+500).";

            var back = SafeReturn(returnUrl);
            if (back != null) return back;

            // ✅ fallback آمن للراديولوجست
            return RedirectToAction(nameof(Requests), new { status = "Pending" });
        }

        // ========================= Result Details =========================
        public async Task<IActionResult> ResultDetails(int id)
        {
            var login = RequireLogin();
            if (login != null) return login;

            var type = CurrentUserType();
            var userId = CurrentUserId();

            var result = await _context.RadiologyResults
                .Include(r => r.Request)
                    .ThenInclude(req => req.Patient)
                .Include(r => r.Request)
                    .ThenInclude(req => req.Doctor)
                .Include(r => r.Radiologist)
                .FirstOrDefaultAsync(r => r.ResultId == id);

            if (result == null) return NotFound();

            if (type == UserType.Patient && result.Request.PatientId != userId)
                return RedirectToAction("AccessDenied", "Account");

            if (type == UserType.Doctor && result.Request.DoctorId != userId)
                return RedirectToAction("AccessDenied", "Account");

            if (type == UserType.Radiologist && result.RadiologistId != userId)
                return RedirectToAction("AccessDenied", "Account");

            return View(result);
        }

        public async Task<IActionResult> PatientResults(int patientId)
        {
            var login = RequireLogin();
            if (login != null) return login;

            var type = CurrentUserType();
            var userId = CurrentUserId();

            if (type == UserType.Patient && patientId != userId)
                return RedirectToAction("AccessDenied", "Account");

            if (type != UserType.Patient && type != UserType.Admin && type != UserType.Radiologist && type != UserType.Doctor)
                return Forbid();

            var results = await _context.RadiologyResults
                .Include(r => r.Request)
                    .ThenInclude(req => req.Patient)
                .Include(r => r.Radiologist)
                .Where(r => r.Request.PatientId == patientId)
                .OrderByDescending(r => r.ResultDate)
                .ToListAsync();

            return View(results);
        }
    }
}
