using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SCMS.Models;
using SCMS.ViewModels;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

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
            // 👇 من برا Register = Patient فقط
            return View(new RegisterVm { UserType = "Patient" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVm vm)
        {
            // ✅ أمان: تجاهل أي قيمة جاية من الفورم
            vm.UserType = "Patient";

            if (!ModelState.IsValid)
                return View(vm);

            if (await _context.Users.AnyAsync(u => u.Username == vm.Username))
            {
                ModelState.AddModelError("", "Username already exists");
                return View(vm);
            }

            string passwordHash = HashPassword(vm.Password);

            // ✅ خارجي = Patient فقط
            User user = new Patient
            {
                Gender = "Unknown",
                Address = "N/A",
                DateOfBirth = DateTime.Today,
                Age = 0,

                FullName = vm.FullName,
                Email = vm.Email,
                Phone = vm.Phone,
                Username = vm.Username,
                PasswordHash = passwordHash,
                IsActive = true
            };

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

        // =========================
        // Forgot / Reset Password (Token + Expiry)
        // =========================

        private string GenerateResetToken()
        {
            return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        }

        private string Sha256Base64(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _context.Users.FirstOrDefaultAsync(u =>
                (u.Email == vm.EmailOrUsername || u.Username == vm.EmailOrUsername) && u.IsActive);

            // رسالة موحدة (أمان) سواء موجود أو لا
            TempData["Message"] = "If the account exists, reset instructions will be sent.";

            if (user == null)
                return RedirectToAction(nameof(Login));

            var rawToken = GenerateResetToken();

            user.PasswordResetTokenHash = Sha256Base64(rawToken);
            user.PasswordResetTokenExpiryUtc = DateTime.UtcNow.AddMinutes(30);

            await _context.SaveChangesAsync();

            var resetUrl = Url.Action(
                "ResetPassword",
                "Account",
                new { email = user.Email, token = rawToken },
                protocol: Request.Scheme);

            // ✅ DEV فقط: اعرض اللينك (بعد كده ابعته Email)
            TempData["ResetLink"] = resetUrl;

            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return RedirectToAction(nameof(Login));

            return View(new ResetPasswordVm
            {
                Email = email,
                Token = token
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == vm.Email && u.IsActive);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid reset link.");
                return View(vm);
            }

            if (user.PasswordResetTokenExpiryUtc == null || user.PasswordResetTokenExpiryUtc < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "Reset link expired. Please request a new one.");
                return View(vm);
            }

            var incomingHash = Sha256Base64(vm.Token);
            if (string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) || user.PasswordResetTokenHash != incomingHash)
            {
                ModelState.AddModelError("", "Invalid reset link.");
                return View(vm);
            }

            user.PasswordHash = HashPassword(vm.NewPassword);

            // invalidate token
            user.PasswordResetTokenHash = null;
            user.PasswordResetTokenExpiryUtc = null;

            await _context.SaveChangesAsync();

            TempData["Message"] = "Password reset successfully. Please login.";
            return RedirectToAction(nameof(Login));
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
