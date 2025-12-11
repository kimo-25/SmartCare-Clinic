using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;

namespace SCMS.Controllers
{
    public class BillingController : Controller
    {
        private readonly AppDbContext _context;

        public BillingController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> PatientInvoices(int patientId)
        {
            var invoices = await _context.Invoices
                .Include(i => i.AppointmentBooking)
                    .ThenInclude(b => b.Patient).ThenInclude(p => p.User)
                .Include(i => i.AppointmentBooking.Appointment)
                    .ThenInclude(a => a.Doctor).ThenInclude(d => d.Staff)
                .Where(i => i.AppointmentBooking.PatientId == patientId)
                .ToListAsync();

            var vm = new PatientInvoicesVm
            {
                PatientId = patientId,
                PatientName = invoices.FirstOrDefault()?.AppointmentBooking.Patient.User.FullName ?? "",
                Invoices = invoices.Select(i => new InvoiceItemVm
                {
                    InvoiceId = i.InvoiceId,
                    BookingId = i.BookingId,
                    PatientName = i.AppointmentBooking.Patient.User.FullName,
                    DoctorName = i.AppointmentBooking.Appointment.Doctor.Staff.EmployeeName,
                    AppointmentDate = i.AppointmentBooking.Appointment.AppointmentDate,
                    TotalAmount = i.TotalAmount,
                    Status = i.Status
                }).ToList()
            };

            return View(vm);
        }

        public async Task<IActionResult> InvoiceDetails(int id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.AppointmentBooking)
                    .ThenInclude(b => b.Patient).ThenInclude(p => p.User)
                .Include(i => i.AppointmentBooking.Appointment)
                    .ThenInclude(a => a.Doctor).ThenInclude(d => d.Staff)
                .FirstOrDefaultAsync(i => i.InvoiceId == id);

            if (invoice == null) return NotFound();

            return View(invoice);
        }

        [HttpGet]
        public IActionResult CreateInvoice(int bookingId)
        {
            var vm = new InvoiceFormVm
            {
                BookingId = bookingId
            };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> CreateInvoice(InvoiceFormVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var invoice = new Invoice
            {
                BookingId = vm.BookingId,
                TotalAmount = vm.TotalAmount,
                Status = vm.Status,
                CreatedAt = DateTime.Now
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(InvoiceDetails), new { id = invoice.InvoiceId });
        }
    }
}
