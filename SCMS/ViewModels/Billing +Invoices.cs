using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SCMS.ViewModels
{
    public class InvoiceItemVm
    {
        public int InvoiceId { get; set; }
        public int BookingId { get; set; }
        public string PatientName { get; set; } = null!;
        public string DoctorName { get; set; } = null!;
        public DateTime AppointmentDate { get; set; }
        public double TotalAmount { get; set; }
        public string Status { get; set; } = null!;
    }

    public class PatientInvoicesVm
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = null!;

        public IEnumerable<InvoiceItemVm> Invoices { get; set; }
            = new List<InvoiceItemVm>();
    }

    public class InvoiceFormVm
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public double TotalAmount { get; set; }

        public string Status { get; set; } = "Not Billed yet";
    }
}
