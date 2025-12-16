using System;

namespace SCMS.ViewModels.Patient
{
    public class PatientVM
    {
        // ===== Basic Info =====
        public int PatientId { get; set; }
        public string FullName { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public DateTime DateOfBirth { get; set; }
        public int Age { get; set; }
        public string Address { get; set; } = null!;
        public string? MedicalHistorySummary { get; set; }
        public bool IsProfileIncomplete { get; set; }

        // ===== Dashboard Stats =====
        public int AppointmentsCount { get; set; }
        public int PrescriptionsCount { get; set; }
        public int MedicalRecordsCount { get; set; }
    }

    public class PatientEditVm
    {
        public int PatientId { get; set; }
        public string? FullName { get; set; }
        public string? Gender { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? MedicalHistorySummary { get; set; }
    }
}
