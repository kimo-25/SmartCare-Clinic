using System;
using System.Collections.Generic;

namespace SCMS.ViewModels
{
    public class UserSummaryVm
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string UserType { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public DateTime DateAdded { get; set; }
        public bool IsActive { get; set; }

    }

    public class AdminDashboardVm
    {
        public string AdminName { get; set; } = null!;

        public int TotalUsers { get; set; }
        public int TotalDoctors { get; set; }
        public int TotalPatients { get; set; }
        public int TodayAppointmentsCount { get; set; }

        public IEnumerable<UserSummaryVm> RecentUsers { get; set; } = new List<UserSummaryVm>();
    }
}
