using Microsoft.AspNetCore.Mvc;
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

        public async Task<IActionResult> Index()
        {
            var feedbacks = await _context.Feedbacks
                .Include(f => f.Patient).ThenInclude(p => p.User)
                .ToListAsync();

            var vm = new FeedbackListVm
            {
                Items = feedbacks.Select(f => new FeedbackItemVm
                {
                    FeedbackId = f.FeedbackId,
                    Name = f.Patient.User.FullName,
                    JobTitle = "", // لو عندك حقل وظيفه ممكن تضيفيه
                    Rate = f.Rate,
                    FeedbackText = f.FeedbackText ?? ""
                }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult Create(int? doctorId)
        {
            var vm = new FeedbackFormVm
            {
                DoctorId = doctorId
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> Create(FeedbackFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            // TODO: هات PatientId الحقيقي من المستخدم الحالي
            var patient = await _context.Patients.FirstOrDefaultAsync();
            if (patient == null)
            {
                ModelState.AddModelError("", "No patient found to attach feedback.");
                return View(vm);
            }

            var feedback = new Feedback
            {
                PatientId = patient.PatientId,
                DoctorId = vm.DoctorId ?? 1, // TODO
                Rate = vm.Rate,
                FeedbackText = vm.FeedbackText,
                CreatedAt = DateTime.Now
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
