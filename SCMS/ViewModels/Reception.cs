using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SCMS.ViewModels
{
    public class ReceptionDashboardVm
    {
        public string ReceptionistName { get; set; } = null!;

        public int TodaysAppointmentsCount { get; set; }

        public IEnumerable<AppointmentSummaryVm> TodaysAppointments { get; set; }
            = new List<AppointmentSummaryVm>();

        public IEnumerable<PatientSummaryVm> RecentPatients { get; set; }
            = new List<PatientSummaryVm>();
    }

    public class PatientListVm
    {
        public string? SearchTerm { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        public IEnumerable<PatientSummaryVm> Patients { get; set; }
            = new List<PatientSummaryVm>();
    }

    public class PatientFormVm
    {
        public int? PatientId { get; set; } // null في Add, فيه قيمة في Edit

        // User info
        [Required]
        public string FullName { get; set; } = null!;

        [Required]
        public string Phone { get; set; } = null!;

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        // Patient info
        [Required]
        public string Gender { get; set; } = null!;

        [Required, DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        public string Address { get; set; } = null!;

        public string? MedicalHistorySummary { get; set; }
    }

    public class PatientHeaderVm   // للبانل الشمال في Patient File لو حبيتي
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = null!;
        public string? PatientCode { get; set; }
        public int Age { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Phone { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? EmergencyContact { get; set; }
        public string? PrimaryPhysician { get; set; }
        public string? InsuranceInfo { get; set; }
        public string? Allergies { get; set; }
    }
}
