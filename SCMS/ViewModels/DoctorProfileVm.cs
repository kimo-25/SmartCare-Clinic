using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SCMS.ViewModels
{
    public class DoctorAppointmentVm
    {
        public int AppointmentId { get; set; }
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public int Capacity { get; set; }
        public int CurrentCount { get; set; }

        public int RemainingSlots => Math.Max(0, Capacity - CurrentCount);

        public double Price { get; set; }

        public string Status { get; set; } = null!;
    }
    public class DoctorEditVm
    {
        public int DoctorId { get; set; }

        [Required]
        public string FullName { get; set; } = "";

        [Required]
        public string Specialization { get; set; } = "";

        [Range(0, 80)]
        public int YearsOfExperience { get; set; }

        [Required]
        public string DepartmentName { get; set; } = "";

        [Required]
        public string PhoneNumber { get; set; } = "";
    }
    public class DoctorProfileVm
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = null!;
        public string Specialization { get; set; } = null!;
        public int YearsOfExperience { get; set; }
        public string DepartmentName { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;

        public double AverageRate { get; set; }
        public bool IsProfileIncomplete { get; set; }
        public int FeedbackCount { get; set; }

        public List<DoctorAppointmentVm> UpcomingAppointments { get; set; } = new();
    }
}
