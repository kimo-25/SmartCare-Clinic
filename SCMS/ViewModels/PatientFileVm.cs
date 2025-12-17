using System;
using System.Collections.Generic;

namespace SCMS.ViewModels.Patient
{
    public class PatientAppointmentRowVm
    {
        public int BookingId { get; set; }
        public int AppointmentId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string DoctorName { get; set; } = "";
        public double Price { get; set; }
        public string BookingStatus { get; set; } = "";
    }

    public class PatientPrescriptionRowVm
    {
        public int PrescriptionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DoctorName { get; set; } = "";
        public string Diagnosis { get; set; } = "";
        public string Treatment { get; set; } = "";
        public bool RadiologyRequested { get; set; }
    }

    public class PatientRadiologyRowVm
    {
        public int ResultId { get; set; }
        public DateTime ResultDate { get; set; }
        public string DoctorName { get; set; } = "";
        public string TestName { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ImagePath { get; set; }
        public string Report { get; set; } = "";
    }

    public class PatientInvoiceLineVm
    {
        public string Title { get; set; } = "";
        public double Amount { get; set; }
    }

    public class PatientInvoiceVm
    {
        public int InvoiceId { get; set; }
        public int BookingId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string DoctorName { get; set; } = "";
        public List<PatientInvoiceLineVm> Lines { get; set; } = new();
        public double Total => Lines.Sum(x => x.Amount);
    }

    public class PatientMedicalFileVm
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = "";

        public List<PatientAppointmentRowVm> Appointments { get; set; } = new();
        public List<PatientPrescriptionRowVm> Prescriptions { get; set; } = new();
        public List<PatientRadiologyRowVm> RadiologyResults { get; set; } = new();
        public List<PatientInvoiceVm> Invoices { get; set; } = new();
    }
}
