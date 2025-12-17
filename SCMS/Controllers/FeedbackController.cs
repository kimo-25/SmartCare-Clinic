using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;

namespace SCMS.Controllers
{
    public class FeedbackController : Controller
    {
        private readonly AppDbContext _context;

        public FeedbackController(AppDbContext context)
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

        // ✅ All Feedbacks
        public async Task<IActionResult> Index()
        {
            var userId = CurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            var feedbacks = await _context.Feedbacks
                .Include(f => f.Patient)
                .Include(f => f.Doctor)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var vm = new FeedbackListVm
            {
                Items = feedbacks.Select(f => new FeedbackItemVm
                {
                    FeedbackId = f.FeedbackId,
                    PatientName = f.Patient.FullName,
                    Rate = f.Rate,
                    FeedbackText = f.FeedbackText ?? "",
                    CreatedAt = f.CreatedAt
                }).ToList()
            };

            return View("AllFeedback", vm);
        }

        // ✅ Add Feedback (Patient only) + load doctors list
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = CurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Patient)
                return Forbid();

            var doctors = await _context.Set<Doctor>()
                .Select(d => new { d.UserId, d.FullName })
                .ToListAsync();

            var vm = new FeedbackFormVm
            {
                Doctors = doctors.Select(d => new SelectListItem
                {
                    Value = d.UserId.ToString(),   // ✅ Doctor UserId
                    Text = d.FullName
                }).ToList()
            };

            return View(vm);
        }

        // ✅ Save Feedback (Patient only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FeedbackFormVm vm)
        {
            var userId = CurrentUserId();
            if (userId == 0)
                return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Patient)
                return Forbid();

            // لازم DoctorId يتعمله validation (عشان dropdown)
            if (vm.DoctorId <= 0)
                ModelState.AddModelError(nameof(vm.DoctorId), "Please select a doctor.");

            if (!ModelState.IsValid)
            {
                // ✅ رجّع قائمة الدكاترة تاني (بدون anonymous type)
                vm.Doctors = await _context.Set<Doctor>()
                    .Select(d => new SelectListItem
                    {
                        Value = d.UserId.ToString(),
                        Text = d.FullName
                    })
                    .ToListAsync();

                return View(vm);
            }

            var patientExists = await _context.Set<Patient>()
                .AnyAsync(p => p.UserId == userId);

            if (!patientExists)
            {
                ModelState.AddModelError("", "هذا المستخدم ليس Patient.");

                // ✅ لازم كمان نرجّع الدكاترة عشان الصفحة متتكسرش
                vm.Doctors = await _context.Set<Doctor>()
                    .Select(d => new SelectListItem
                    {
                        Value = d.UserId.ToString(),
                        Text = d.FullName
                    })
                    .ToListAsync();

                return View(vm);
            }

            var doctorExists = await _context.Set<Doctor>()
                .AnyAsync(d => d.UserId == vm.DoctorId);

            if (!doctorExists)
            {
                ModelState.AddModelError("", "Doctor غير موجود.");

                // ✅ لازم كمان نرجّع الدكاترة
                vm.Doctors = await _context.Set<Doctor>()
                    .Select(d => new SelectListItem
                    {
                        Value = d.UserId.ToString(),
                        Text = d.FullName
                    })
                    .ToListAsync();

                return View(vm);
            }

            var feedback = new Feedback
            {
                PatientId = userId,      // ✅ Patient UserId
                DoctorId = vm.DoctorId,  // ✅ Doctor UserId
                Rate = vm.Rate,
                FeedbackText = vm.FeedbackText,
                CreatedAt = DateTime.UtcNow
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}