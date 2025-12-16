using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SCMS.ViewModels
{
    public class RadiologyRequestItemVm
    {
        public int RequestId { get; set; }

        public int? ResultId { get; set; }

        public string PatientName { get; set; } = null!;

        public int Age { get; set; }

        public string Phone { get; set; } = null!;

        public DateTime RequestDate { get; set; }

        public string TestName { get; set; } = null!;

        public string Status { get; set; } = null!;
    }

    public class RadiologyRequestListVm
    {
        public string? SearchTerm { get; set; }

        public int CurrentPage { get; set; }

        public int TotalPages { get; set; }

        public IEnumerable<RadiologyRequestItemVm> Requests { get; set; }
            = new List<RadiologyRequestItemVm>();
    }

    public class RadiologyRequestFormVm
    {
        [Required]
        public int PatientId { get; set; }

        [Required]
        public int DoctorId { get; set; }

        [Required]
        [Display(Name = "Radiology Test")]
        public string TestName { get; set; } = null!;

        [Display(Name = "Clinical Notes")]
        public string? ClinicalNotes { get; set; }

        public int? PrescriptionId { get; set; }
    }

    public class RadiologyResultFormVm
    {
        [Required]
        public int RequestId { get; set; }

        public int RadiologistId { get; set; }

        [Display(Name = "Ray File")]
        public IFormFile? RayFile { get; set; }

        [Required]
        public string Report { get; set; } = null!;

        public string Status { get; set; } = "Completed";

        public string? ImagePath { get; set; }
    }
}
