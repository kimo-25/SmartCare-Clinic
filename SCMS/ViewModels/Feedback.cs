using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SCMS.ViewModels
{
    public class FeedbackFormVm
    {
        [Required]
        public string Name { get; set; } = null!;

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Range(1, 5)]
        public int Rate { get; set; }

        public string? FeedbackText { get; set; }

        public int? DoctorId { get; set; }   // اختياري لو عايزة تربطيها بدكتور
    }

    public class FeedbackItemVm
    {
        public int FeedbackId { get; set; }
        public string Name { get; set; } = null!;
        public string JobTitle { get; set; } = "";
        public int Rate { get; set; }
        public string FeedbackText { get; set; } = null!;
    }

    public class FeedbackListVm
    {
        public IEnumerable<FeedbackItemVm> Items { get; set; }
            = new List<FeedbackItemVm>();
    }
}
