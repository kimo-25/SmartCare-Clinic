namespace SCMS.ViewModels
{
    public class PatientProfileRecordVm
    {
        public int RecordId { get; set; }
        public DateTime RecordDate { get; set; }
        public string? Description { get; set; }

        public string? Diagnosis { get; set; }
        public string? Treatment { get; set; }

        public string? RadiologyTestName { get; set; }
        public string? RadiologyStatus { get; set; }
    }

    public class PatientProfileVm
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = null!;
        public int Age { get; set; }
        public string Gender { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? MedicalHistorySummary { get; set; }
        public bool IsProfileIncomplete { get; set; }
        public List<PatientProfileRecordVm> Records { get; set; } = new();
    }
}
