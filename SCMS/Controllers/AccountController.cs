using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Http;

namespace SCMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // ================= REGISTER =================
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterVm());
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            if (await _context.Users.AnyAsync(u => u.Username == vm.Username))
            {
                ModelState.AddModelError("", "Username already exists");
                return View(vm);
            }

            string passwordHash = HashPassword(vm.Password);

            // ✅ Create user based on type + fill required fields (TPH NOT NULL columns)
            SCMS.Models.User user = vm.UserType switch
            {
                "Patient" => new Patient
                {
                    Gender = "Unknown",
                    Address = "N/A",
                    DateOfBirth = DateTime.Today,
                    Age = 0
                },

                "Doctor" => new Doctor
                {
                    DepartmentName = "General",
                    PhoneNumber = vm.Phone,       // مهم جداً لأن Staff.PhoneNumber Required
                    Specialization = "General",   // مهم جداً لأن Doctor.Specialization Required
                    YearsOfExperience = 0,
                    Salary = 0
                },

                "Receptionist" => new Receptionist
                {
                    DepartmentName = "Reception",
                    PhoneNumber = vm.Phone,
                    Shift = "Morning",
                    Salary = 0
                },

                "Admin" => new Admin
                {
                    AccessLevel = "Full"
                },

                _ => new SCMS.Models.User()
            };

            // Common fields
            user.FullName = vm.FullName;
            user.Email = vm.Email;
            user.Phone = vm.Phone;
            user.Username = vm.Username;
            user.PasswordHash = passwordHash;
            user.IsActive = true;

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", ex.InnerException?.Message ?? ex.Message);
                return View(vm);
            }

            return RedirectToAction(nameof(Login));
        }

        // ================= LOGIN =================
        [HttpGet]
        public IActionResult Login()
        {
            return View(new LoginVm());
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _context.Users
                .FirstOrDefaultAsync(u =>
                    (u.Email == vm.EmailOrUsername || u.Username == vm.EmailOrUsername)
                    && u.IsActive);

            if (user == null || !VerifyPassword(vm.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid credentials");
                return View(vm);
            }

            var discriminator = _context.Entry(user)
                .Property("Discriminator")
                .CurrentValue?.ToString() ?? "User";

            // Save session
            HttpContext.Session.SetString("UserId", user.UserId.ToString());
            HttpContext.Session.SetString("UserType", discriminator);

            // Redirect based on user type
            return discriminator switch
            {
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                "Doctor" => RedirectToAction("Dashboard", "Doctor"),
                "Receptionist" => RedirectToAction("Dashboard", "Reception"),
                "Radiologist" => RedirectToAction("Requests", "Radiology"),
                "Patient" => RedirectToAction("Index", "Home"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        // ================= LOGOUT =================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction(nameof(Login));
        }

        // ================= PASSWORD HASHING =================
        private string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            string hash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password,
                salt,
                KeyDerivationPrf.HMACSHA256,
                100000,
                32));

            return $"{Convert.ToBase64String(salt)}.{hash}";
        }

        private bool VerifyPassword(string password, string hash)
        {
            var parts = hash.Split('.');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var stored = parts[1];

            var computed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password,
                salt,
                KeyDerivationPrf.HMACSHA256,
                100000,
                32));

            return computed == stored;
        }

        // ================= ACCESS DENIED =================
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ================= PROTECTED ADMIN PAGE EXAMPLE =================
        public IActionResult SomeAdminPage()
        {
            var userType = HttpContext.Session.GetString("UserType");
            if (userType != "Admin")
            {
                return RedirectToAction("AccessDenied", "Account");
            }
            return View();
        }

        // ================= FORGOT PASSWORD =================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(string EmailOrUsername)
        {
            if (string.IsNullOrEmpty(EmailOrUsername))
            {
                TempData["Message"] = "Please enter your email or username.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == EmailOrUsername || u.Username == EmailOrUsername);

            if (user == null)
            {
                TempData["Message"] = "User not found.";
                return View();
            }

            return RedirectToAction("ResetPassword", new { userId = user.UserId });
        }

        // ================= RESET PASSWORD =================
        [HttpGet]
        public IActionResult ResetPassword(int userId)
        {
            var vm = new ResetPasswordVm { UserId = userId };
            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            if (vm.NewPassword != vm.ConfirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match");
                return View(vm);
            }

            var user = await _context.Users.FindAsync(vm.UserId);
            if (user == null)
            {
                ModelState.AddModelError("", "User not found");
                return View(vm);
            }

            user.PasswordHash = HashPassword(vm.NewPassword);
            _context.Update(user);
            await _context.SaveChangesAsync();

            TempData["Message"] = "Password reset successfully!";
            return RedirectToAction("Login");
        }
    }
}
