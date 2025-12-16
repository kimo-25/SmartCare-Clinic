using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
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

        private int CurrentUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            return int.TryParse(userIdStr, out var id) ? id : 0;
        }

        private UserType CurrentUserType()
        {
            var t = HttpContext.Session.GetInt32("UserType");
            return t.HasValue ? (UserType)t.Value : UserType.User;
        }

        private IActionResult RequireAdmin()
        {
            var userId = CurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Admin)
                return RedirectToAction("AccessDenied", "Account");

            return null!;
        }

        // ================= Dashboard =================
        public async Task<IActionResult> Dashboard()
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var vm = new AdminDashboardVm
            {
                AdminName = "Admin",
                TotalUsers = await _context.Users.CountAsync(),
                TotalDoctors = await _context.Set<Doctor>().CountAsync(),
                TotalPatients = await _context.Set<Patient>().CountAsync(),
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
                        UserType = EF.Property<string>(u, "Discriminator"),
                        DateAdded = u.CreatedAt
                    })
                    .ToListAsync()
            };

            return View(vm);
        }

        // ================= Users List =================
        public async Task<IActionResult> Users()
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var users = await _context.Users
                .OrderBy(u => u.FullName)
                .Select(u => new UserSummaryVm
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    Phone = u.Phone!,
                    UserType = EF.Property<string>(u, "Discriminator"),
                    DateAdded = u.CreatedAt
                })
                .ToListAsync();

            return View(users);
        }

        // ================= Appointments Overview =================
        public async Task<IActionResult> AppointmentsOverview()
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Bookings)
                    .ThenInclude(b => b.Patient)
                .OrderByDescending(a => a.AppointmentDate)
                .ToListAsync();

            return View(appointments);
        }

        // ================= Create User =================
        [HttpGet]
        public IActionResult CreateUser()
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            return View(new RegisterVm());
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(RegisterVm vm)
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            if (!ModelState.IsValid)
                return View(vm);

            if (await _context.Users.AnyAsync(u => u.Username == vm.Username))
            {
                ModelState.AddModelError("", "Username already exists");
                return View(vm);
            }

            // Hash Password
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                vm.Password,
                salt,
                KeyDerivationPrf.HMACSHA256,
                100000,
                32
            ));
            var passwordHash = $"{Convert.ToBase64String(salt)}.{hash}";

            // ✅ TPH: النوع بيتحدد من الكلاس
            SCMS.Models.User dbUser = vm.UserType switch
            {
                "Admin" => new Admin(),
                "Doctor" => new Doctor(),
                "Receptionist" => new Receptionist(),
                "Radiologist" => new Radiologist(),
                "Patient" => new Patient(),
                _ => new SCMS.Models.User()
            };

            dbUser.FullName = vm.FullName;
            dbUser.Email = vm.Email;
            dbUser.Phone = vm.Phone;
            dbUser.Username = vm.Username;
            dbUser.PasswordHash = passwordHash;
            dbUser.IsActive = true;

            _context.Users.Add(dbUser);
            await _context.SaveChangesAsync();

            TempData["Message"] = "User created successfully!";
            return RedirectToAction("Users");
        }

        // ================= Edit User =================
        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var dbUser = await _context.Users.FindAsync(id);
            if (dbUser == null) return NotFound();

            return View(dbUser);
        }

        [HttpPost]
        public async Task<IActionResult> EditUser(SCMS.Models.User model, string UserType)
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var dbUser = await _context.Users.FindAsync(model.UserId);
            if (dbUser == null) return NotFound();

            dbUser.FullName = model.FullName;
            dbUser.Email = model.Email;
            dbUser.Phone = model.Phone;

            // تغيير نوع المستخدم إذا لزم الأمر (TPH)
            if (!string.IsNullOrEmpty(UserType) && dbUser.GetType().Name != UserType)
            {
                SCMS.Models.User newUser = UserType switch
                {
                    "Admin" => new Admin(),
                    "Doctor" => new Doctor(),
                    "Receptionist" => new Receptionist(),
                    "Radiologist" => new Radiologist(),
                    "Patient" => new Patient(),
                    _ => dbUser
                };

                newUser.UserId = dbUser.UserId;
                newUser.FullName = dbUser.FullName;
                newUser.Email = dbUser.Email;
                newUser.Phone = dbUser.Phone;
                newUser.Username = dbUser.Username;
                newUser.PasswordHash = dbUser.PasswordHash;
                newUser.IsActive = dbUser.IsActive;
                newUser.CreatedAt = dbUser.CreatedAt;

                _context.Entry(dbUser).State = EntityState.Detached;
                _context.Users.Update(newUser);
            }
            else
            {
                _context.Users.Update(dbUser);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Users");
        }
    }
}
