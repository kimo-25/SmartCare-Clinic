using SCMS.Models;
using SCMS.ViewModels;
using System;
using System.Collections.Generic;

namespace SCMS.ViewModels
{
    public class AppointmentSummaryVm
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; } = null!;
        public string DoctorName { get; set; } = null!;
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public string Status { get; set; } = null!;
    }

    public class PatientSummaryVm
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = null!;
        public int Age { get; set; }
        public string Phone { get; set; } = null!;
        public DateTime? LastVisit { get; set; }
    }

    public class NotificationVm
    {
        public string Title { get; set; } = null!;
        public string Message { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string? LinkAction { get; set; }
        public string? LinkController { get; set; }
        public int? RelatedId { get; set; }
    }
}
