using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;

namespace SCMS.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var vm = new AdminDashboardVm
            {
                AdminName = User.Identity?.Name ?? "Admin",
                TotalUsers = await _context.Users.CountAsync(),
                TotalDoctors = await _context.Doctors.CountAsync(),
                TotalPatients = await _context.Patients.CountAsync(),
                TodayAppointmentsCount = await _context.Appointments
                    .CountAsync(a => a.AppointmentDate.Date == DateTime.Today),
                RecentUsers = await _context.Users
                    .OrderByDescending(u => u.UserId)
                    .Take(5)
                    .Select(u => new UserSummaryVm
                    {
                        UserId = u.UserId,
                        FullName = u.FullName,
                        Email = u.Email,
                        Role = u.Role,
                        DateAdded = u.CreatedAt
                    }).ToListAsync()
            };

            return View(vm);
        }

        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .OrderBy(u => u.FullName)
                .Select(u => new UserSummaryVm
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Role = u.Role,
                    DateAdded = u.CreatedAt
                }).ToListAsync();

            return View(users);
        }

        [HttpGet]
        public IActionResult CreateUser()
        {
            return View(new RegisterVm());
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(RegisterVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = new User
            {
                FullName = vm.FullName,
                Email = vm.Email,
                Phone = vm.Phone,
                Username = vm.Username,
                PasswordHash = vm.Password, // TODO: Hash
                Role = vm.Role,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var vm = new RegisterVm
            {
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                Username = user.Username,
                Role = user.Role
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(int id, RegisterVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.FullName = vm.FullName;
            user.Email = vm.Email;
            user.Phone = vm.Phone;
            user.Username = vm.Username;
            user.Role = vm.Role;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Users));
        }

        public IActionResult Roles()
        {
            // لو هتعملي Roles ثابتة ممكن تعرضيها هنا
            return View();
        }

        public IActionResult Reports()
        {
            return View();
        }

        public IActionResult ActivityLogs()
        {
            return View();
        }

        public async Task<IActionResult> AppointmentsOverview()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.Staff)
                .ToListAsync();

            return View(appointments);
        }
    }
}
