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

        private IActionResult? RequireAdmin()
        {
            var userId = CurrentUserId();
            if (userId == 0) return RedirectToAction("Login", "Account");

            if (CurrentUserType() != UserType.Admin)
                return RedirectToAction("AccessDenied", "Account");

            return null;
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
                        Phone = u.Phone!,
                        UserType = EF.Property<string>(u, "Discriminator"),
                        DateAdded = u.CreatedAt,
                        IsActive = u.IsActive
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
                    DateAdded = u.CreatedAt,
                    IsActive = u.IsActive
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(RegisterVm vm)
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Admin", "Doctor", "Receptionist", "Radiologist", "Patient"
            };

            if (string.IsNullOrWhiteSpace(vm.UserType) || !allowed.Contains(vm.UserType))
                ModelState.AddModelError(nameof(vm.UserType), "Please select a valid user type.");

            if (!ModelState.IsValid)
                return View(vm);

            if (await _context.Users.AnyAsync(u => u.Username == vm.Username))
            {
                ModelState.AddModelError("", "Username already exists");
                return View(vm);
            }

            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                vm.Password,
                salt,
                KeyDerivationPrf.HMACSHA256,
                100000,
                32
            ));
            var passwordHash = $"{Convert.ToBase64String(salt)}.{hash}";

            User dbUser = vm.UserType switch
            {
                "Admin" => new Admin(),
                "Doctor" => new Doctor(),
                "Receptionist" => new Receptionist(),
                "Radiologist" => new Radiologist(),
                "Patient" => new Patient(),
                _ => new User()
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
            return RedirectToAction(nameof(Users));
        }

        // ================= Edit User =================
        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
            if (dbUser == null) return NotFound();

            return View(dbUser);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(User model, string userType)
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == model.UserId);
            if (dbUser == null) return NotFound();

            // ✅ تحديث البيانات المشتركة
            dbUser.FullName = model.FullName;
            dbUser.Email = model.Email;
            dbUser.Phone = model.Phone;

            // ✅ مهم: حفظ IsActive من الفورم
            dbUser.IsActive = model.IsActive;

            var currentType = dbUser.GetType().Name;

            // لو النوع ما اتغيرش
            if (string.Equals(currentType, userType, StringComparison.OrdinalIgnoreCase))
            {
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Users));
            }

            // ✅ النوع اتغير: Remove + Add
            var newUser = userType switch
            {
                "Admin" => new Admin(),
                "Doctor" => new Doctor(),
                "Receptionist" => new Receptionist(),
                "Radiologist" => new Radiologist(),
                "Patient" => new Patient(),
                _ => new User()
            };

            newUser.UserId = dbUser.UserId;
            newUser.FullName = dbUser.FullName;
            newUser.Email = dbUser.Email;
            newUser.Phone = dbUser.Phone;
            newUser.Username = dbUser.Username;
            newUser.PasswordHash = dbUser.PasswordHash;

            // ✅ خد الحالة الجديدة اللي جاية من الفورم
            newUser.IsActive = model.IsActive;

            newUser.CreatedAt = dbUser.CreatedAt;

            _context.Users.Remove(dbUser);
            await _context.SaveChangesAsync();

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Users));
        }

        // ================= Delete User =================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var guard = RequireAdmin();
            if (guard != null) return guard;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();

            var hasRadiologyRefs = await _context.Set<RadiologyRequest>()
                .AnyAsync(r => r.RadiologistId == id);

            if (hasRadiologyRefs)
            {
                user.IsActive = false;
                await _context.SaveChangesAsync();

                TempData["Message"] = "User cannot be deleted because it has related radiology requests. User was deactivated instead.";
                return RedirectToAction(nameof(Users));
            }

            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                TempData["Message"] = "User deleted successfully!";
                return RedirectToAction(nameof(Users));
            }
            catch (DbUpdateException)
            {
                user.IsActive = false;
                await _context.SaveChangesAsync();

                TempData["Message"] = "User cannot be deleted because it has related data. User was deactivated instead.";
                return RedirectToAction(nameof(Users));
            }
        }
    }
}
