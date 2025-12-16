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

        private async Task<PatientVM?> GetPatientVM(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.AppointmentBookings)
                .Include(p => p.Prescriptions)
                .Include(p => p.MedicalRecords)
                .FirstOrDefaultAsync(p => p.UserId == id);

            if (patient == null)
                return null;

            return new PatientVM
            {
                FullName = patient.FullName,
                Gender = patient.Gender,
                DateOfBirth = patient.DateOfBirth,
                Age = patient.Age,
                Address = patient.Address,
                MedicalHistorySummary = patient.MedicalHistorySummary,

                AppointmentsCount = patient.AppointmentBookings.Count,
                PrescriptionsCount = patient.Prescriptions.Count,
                MedicalRecordsCount = patient.MedicalRecords.Count
            };
        }

        public async Task<IActionResult> Dashboard(int id)
        {
            var vm = await GetPatientVM(id);
            if (vm == null) return NotFound();

            return View(vm);
        }

        public async Task<IActionResult> Profile(int id)
        {
            var vm = await GetPatientVM(id);
            if (vm == null) return NotFound();

            return View(vm);
        }
    }
}
