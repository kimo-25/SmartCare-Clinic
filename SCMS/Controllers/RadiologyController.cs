using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;

namespace SCMS.Controllers
{
    public class RadiologyController : Controller
    {
        private readonly AppDbContext _context;

        public RadiologyController(AppDbContext context)
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

        public async Task<IActionResult> Requests()
        {
            var userId = CurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            var type = CurrentUserType();
            if (type != UserType.Radiologist && type != UserType.Admin && type != UserType.Receptionist)
                return Forbid();

            var requests = await _context.RadiologyRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .Include(r => r.Radiologist)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            var vm = new RadiologyRequestListVm
            {
                Requests = requests.Select(r => new RadiologyRequestItemVm
                {
                    RequestId = r.RequestId,
                    PatientName = r.Patient.FullName,
                    Age = r.Patient.Age,
                    Phone = r.Patient.Phone!,
                    RequestDate = r.RequestDate,
                    TestName = r.TestName,
                    Status = r.Status
                }).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult CreateRequest(int patientId, int doctorId)
        {
            return View(new RadiologyRequestFormVm
            {
                PatientId = patientId,
                DoctorId = doctorId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRequest(RadiologyRequestFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var patientExists = await _context.Set<Patient>().AnyAsync(p => p.UserId == vm.PatientId);
            var doctorExists = await _context.Set<Doctor>().AnyAsync(d => d.UserId == vm.DoctorId);

            if (!patientExists || !doctorExists)
            {
                ModelState.AddModelError("", "Patient أو Doctor غير موجود.");
                return View(vm);
            }

            var request = new RadiologyRequest
            {
                PatientId = vm.PatientId,
                DoctorId = vm.DoctorId,
                TestName = vm.TestName,
                ClinicalNotes = vm.ClinicalNotes,
                PrescriptionId = vm.PrescriptionId,
                Status = "Pending",
                RequestDate = DateTime.UtcNow
            };

            _context.RadiologyRequests.Add(request);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Requests));
        }

        public async Task<IActionResult> RequestDetails(int id)
        {
            var request = await _context.RadiologyRequests
                .Include(r => r.Patient)
                .Include(r => r.Doctor)
                .Include(r => r.Radiologist)
                .Include(r => r.Result)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            return View(request);
        }

        [HttpGet]
        public IActionResult CreateResult(int requestId)
        {
            var userId = CurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Radiologist)
                return Forbid();

            return View(new RadiologyResultFormVm
            {
                RequestId = requestId,
                RadiologistId = userId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateResult(RadiologyResultFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var req = await _context.RadiologyRequests.FirstOrDefaultAsync(r => r.RequestId == vm.RequestId);
            if (req == null) return NotFound();

            var radiologistExists = await _context.Set<Radiologist>().AnyAsync(r => r.UserId == vm.RadiologistId);
            if (!radiologistExists)
            {
                ModelState.AddModelError("", "Radiologist غير موجود.");
                return View(vm);
            }

            var alreadyHasResult = await _context.RadiologyResults.AnyAsync(x => x.RequestId == vm.RequestId);
            if (alreadyHasResult)
            {
                ModelState.AddModelError("", "هذا الطلب له نتيجة بالفعل.");
                return View(vm);
            }

            var result = new RadiologyResult
            {
                RequestId = vm.RequestId,
                RadiologistId = vm.RadiologistId,
                ImagePath = vm.ImagePath,
                Report = vm.Report,
                Status = string.IsNullOrWhiteSpace(vm.Status) ? "Completed" : vm.Status,
                ResultDate = DateTime.UtcNow
            };

            _context.RadiologyResults.Add(result);

            req.Status = "Completed";
            req.RadiologistId = vm.RadiologistId;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(RequestDetails), new { id = vm.RequestId });
        }

        public async Task<IActionResult> ResultDetails(int id)
        {
            var result = await _context.RadiologyResults
                .Include(r => r.Request)
                    .ThenInclude(req => req.Patient)
                .Include(r => r.Radiologist)
                .FirstOrDefaultAsync(r => r.ResultId == id);

            if (result == null) return NotFound();

            return View(result);
        }

        public async Task<IActionResult> PatientResults(int patientId)
        {
            var results = await _context.RadiologyResults
                .Include(r => r.Request)
                .Where(r => r.Request.PatientId == patientId)
                .OrderByDescending(r => r.ResultDate)
                .ToListAsync();

            return View(results);
        }
    }
}
