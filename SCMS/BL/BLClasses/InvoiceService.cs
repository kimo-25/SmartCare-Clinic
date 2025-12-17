using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SCMS.BL.BLInterfaces;
using SCMS.Models;

namespace SCMS.BL.BLClasses
{
    public class InvoiceService : IInvoiceService
    {
        private readonly AppDbContext _context;

        public InvoiceService(AppDbContext context)
        {
            _context = context;
        }

        public Invoice? CreateForBooking(int bookingId)
        {
            var booking = _context.AppointmentBookings
                .Include(b => b.Appointment)
                .FirstOrDefault(b => b.BookingId == bookingId);

            if (booking == null) return null;
            if (_context.Invoices.Any(i => i.BookingId == bookingId)) return null;
            if (booking.Status != "Booked") return null;

            var invoice = new Invoice
            {
                BookingId = bookingId,
                TotalAmount = booking.Appointment.Price,
                Status = "Not Billed yet"
            };

            _context.Invoices.Add(invoice);
            _context.SaveChanges();

            return invoice;
        }

        public Invoice? GetById(int invoiceId)
        {
            return _context.Invoices
                .Include(i => i.AppointmentBooking)
                    .ThenInclude(b => b.Appointment)
                .FirstOrDefault(i => i.InvoiceId == invoiceId);
        }

        public Invoice? GetByBooking(int bookingId)
        {
            return _context.Invoices
                .Include(i => i.AppointmentBooking)
                    .ThenInclude(b => b.Appointment)
                .FirstOrDefault(i => i.BookingId == bookingId);
        }

        public List<Invoice> GetByPatient(int patientId)
        {
            return _context.Invoices
                .Include(i => i.AppointmentBooking)
                    .ThenInclude(b => b.Appointment)
                .Where(i => i.AppointmentBooking.PatientId == patientId)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
        }
        public bool AddRadiologyFeeForCompletedResult(int requestId, double feePerResult = 500)
        {
            // هات الريكويست + الدكتور + المريض + النتيجة
            var req = _context.RadiologyRequests
                .Include(r => r.Result)
                .FirstOrDefault(r => r.RequestId == requestId);

            if (req == null) return false;
            if (req.Result == null) return false;
            if (req.Result.Status != "Completed") return false;

            // هات آخر booking للمريض عند نفس الدكتور
            var booking = _context.AppointmentBookings
                .Include(b => b.Invoice)
                .Include(b => b.Appointment)
                .Where(b =>
                    b.PatientId == req.PatientId &&
                    b.Appointment.DoctorId == req.DoctorId &&
                    b.Status == "Booked")
                .OrderByDescending(b => b.CreatedAt)
                .FirstOrDefault();

            if (booking == null) return false;

            // لو مفيش invoice اعمل واحدة
            var invoice = booking.Invoice;
            if (invoice == null)
            {
                invoice = new Invoice
                {
                    BookingId = booking.BookingId,
                    TotalAmount = booking.Appointment.Price,
                    Status = "Not Billed yet"
                };
                _context.Invoices.Add(invoice);
                _context.SaveChanges();
            }

            // ✅ زوّد 500
            invoice.TotalAmount += feePerResult;
            invoice.Status = "Updated (Radiology)";
            _context.SaveChanges();

            return true;
        }

        public bool MarkAsPaid(int invoiceId)
        {
            var invoice = _context.Invoices.FirstOrDefault(i => i.InvoiceId == invoiceId);
            if (invoice == null) return false;

            invoice.Status = "Paid";
            _context.SaveChanges();
            return true;
        }
    }
}
