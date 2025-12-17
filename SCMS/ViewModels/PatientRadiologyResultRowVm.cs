using System;
using System.Collections.Generic;

namespace SCMS.ViewModels.Patient
{
    public class PatientRadiologyResultRowVm
    {
        public int ResultId { get; set; }
        public int RequestId { get; set; }

        public DateTime ResultDate { get; set; }

        public string TestName { get; set; } = "";
        public string DoctorName { get; set; } = "";
        public string RadiologistName { get; set; } = "";

        public string Report { get; set; } = "";
        public string? ImagePath { get; set; }
    }

    public class PatientRadiologyResultsVm
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = "";

        public List<PatientRadiologyResultRowVm> Results { get; set; } = new();
    }
}
