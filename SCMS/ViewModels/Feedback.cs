using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SCMS.ViewModels
{
    public class FeedbackFormVm
    {
        [Range(1, 5, ErrorMessage = "Rate must be between 1 and 5")]
        public int Rate { get; set; }

        [MaxLength(1000)]
        public string? FeedbackText { get; set; }

        public int DoctorId { get; set; }

        // ✅ دي الجديدة (قائمة الدكاترة للـ dropdown)
        public List<SelectListItem> Doctors { get; set; } = new();
    }

    public class FeedbackItemVm
    {
        public int FeedbackId { get; set; }
        public string PatientName { get; set; } = null!;
        public int Rate { get; set; }
        public string FeedbackText { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class FeedbackListVm
    {
        public IEnumerable<FeedbackItemVm> Items { get; set; } = new List<FeedbackItemVm>();
    }
}
