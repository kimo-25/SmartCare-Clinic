using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SCMS.ViewModels
{
    public class ReceptionDashboardVm
    {
        public string ReceptionistName { get; set; } = "Receptionist";

        public int TodaysAppointmentsCount { get; set; }

        // ✅ للكروت
        public int RecentPatientsCount { get; set; }
        public int RadiologyRequestsCount { get; set; }

        public IEnumerable<AppointmentSummaryVm> TodaysAppointments { get; set; }
            = new List<AppointmentSummaryVm>();

        public IEnumerable<PatientSummaryVm> RecentPatients { get; set; }
            = new List<PatientSummaryVm>();
    }

    public class PatientListVm
    {
        public string? SearchTerm { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;

        public IEnumerable<PatientSummaryVm> Patients { get; set; }
            = new List<PatientSummaryVm>();
    }

    public class PatientFormVm
    {
        public int? PatientId { get; set; }

        [Required, MaxLength(150)]
        public string FullName { get; set; } = null!;

        [Required, MaxLength(20)]
        public string Phone { get; set; } = null!;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = null!;

        [Required, MaxLength(20)]
        public string Gender { get; set; } = null!;

        [Required, DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required, MaxLength(250)]
        public string Address { get; set; } = null!;

        [MaxLength(1000)]
        public string? MedicalHistorySummary { get; set; }
    }

    public class PatientHeaderVm
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = null!;
        public int Age { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Phone { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? Allergies { get; set; }
    }
}
