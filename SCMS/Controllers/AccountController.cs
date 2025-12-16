using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace SCMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        private string GetDiscriminator(User user)
        {
            return _context.Entry(user)
                .Property("Discriminator")
                .CurrentValue?
                .ToString() ?? "User";
        }

        private UserType MapDiscriminatorToUserType(string discriminator)
        {
            return discriminator switch
            {
                "Admin" => UserType.Admin,
                "Doctor" => UserType.Doctor,
                "Receptionist" => UserType.Receptionist,
                "Radiologist" => UserType.Radiologist,
                "Patient" => UserType.Patient,
                "Staff" => UserType.Staff,
                _ => UserType.User
            };
        }

        private static string NormalizeUserType(string? s)
        {
            return (s ?? "").Trim();
        }

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

            User user = vm.UserType switch
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
                    PhoneNumber = vm.Phone,
                    Specialization = "General",
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

                "Radiologist" => new Radiologist
                {
                    DepartmentName = "Radiology",
                    PhoneNumber = vm.Phone,
                    Salary = 0
                },

                "Admin" => new Admin
                {
                    AccessLevel = "Full"
                },

                _ => new User()
            };

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

            var discriminator = GetDiscriminator(user);
            var realUserTypeEnum = MapDiscriminatorToUserType(discriminator);
            var selected = NormalizeUserType(vm.UserType);

            if (!string.Equals(selected, discriminator, StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("AccessDenied", "Account");

            HttpContext.Session.SetString("UserId", user.UserId.ToString());
            HttpContext.Session.SetInt32("UserType", (int)realUserTypeEnum);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username ?? user.Email ?? "user"),
                new Claim(ClaimTypes.Role, discriminator)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(6)
                });

            return realUserTypeEnum switch
            {
                UserType.Admin => RedirectToAction("Dashboard", "Admin"),
                UserType.Doctor => RedirectToAction("Dashboard", "Doctor"),
                UserType.Receptionist => RedirectToAction("Dashboard", "Reception"),
                UserType.Radiologist => RedirectToAction("Requests", "Radiology"),
                UserType.Patient => RedirectToAction("Dashboard", "Patient", new { id = user.UserId }),
                _ => RedirectToAction("Index", "Home")
            };
        }

        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

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

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string EmailOrUsername)
        {
            if (string.IsNullOrWhiteSpace(EmailOrUsername))
            {
                ModelState.AddModelError("", "Please enter email or username");
                return View();
            }

            await _context.Users.FirstOrDefaultAsync(u =>
                (u.Email == EmailOrUsername || u.Username == EmailOrUsername) && u.IsActive);

            TempData["Message"] = "If the account exists, reset instructions will be sent.";
            return RedirectToAction(nameof(Login));
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
