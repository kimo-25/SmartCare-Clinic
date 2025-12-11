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

        public async Task<IActionResult> Requests()
        {
            var requests = await _context.RadiologyRequests
                .Include(r => r.Patient).ThenInclude(p => p.User)
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

        [HttpGet]
        public IActionResult CreateRequest(int patientId, int doctorId)
        {
            var vm = new RadiologyRequestFormVm
            {
                PatientId = patientId,
                DoctorId = doctorId
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRequest(RadiologyRequestFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

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
                .Include(r => r.Patient).ThenInclude(p => p.User)
                .Include(r => r.Doctor).ThenInclude(d => d.Staff)
                .Include(r => r.Radiologist).ThenInclude(rad => rad.Staff)
                .Include(r => r.Result)
                .FirstOrDefaultAsync(r => r.RequestId == id);

            if (request == null) return NotFound();

            return View(request);
        }

        [HttpGet]
        public IActionResult CreateResult(int requestId, int radiologistId)
        {
            var vm = new RadiologyResultFormVm
            {
                RequestId = requestId,
                RadiologistId = radiologistId
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CreateResult(RadiologyResultFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var result = new RadiologyResult
            {
                RequestId = vm.RequestId,
                RadiologistId = vm.RadiologistId,
                ImagePath = vm.ImagePath,
                Report = vm.Report,
                Status = vm.Status,
                ResultDate = DateTime.UtcNow
            };

            _context.RadiologyResults.Add(result);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(RequestDetails), new { id = vm.RequestId });
        }

        public async Task<IActionResult> ResultDetails(int id)
        {
            var result = await _context.RadiologyResults
                .Include(r => r.Request).ThenInclude(req => req.Patient).ThenInclude(p => p.User)
                .Include(r => r.Radiologist).ThenInclude(rad => rad.Staff)
                .FirstOrDefaultAsync(r => r.ResultId == id);

            if (result == null) return NotFound();

            return View(result);
        }

        public async Task<IActionResult> PatientResults(int patientId)
        {
            var results = await _context.RadiologyResults
                .Include(r => r.Request)
                .Where(r => r.Request.PatientId == patientId)
                .ToListAsync();

            return View(results);
        }
    }
}
