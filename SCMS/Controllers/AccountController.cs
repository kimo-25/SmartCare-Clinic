using Microsoft.AspNetCore.Mvc;
using SCMS.Models;
using SCMS.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace SCMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
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

            var user = new User
            {
                FullName = vm.FullName,
                Email = vm.Email,
                Phone = vm.Phone,
                Username = vm.Username,
                PasswordHash = vm.Password, // TODO: اعملي Hash حقيقي
                Role = vm.Role,
                IsActive = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // TODO: ممكن تعملي SignIn هنا
            return RedirectToAction("Login");
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
                    (u.Email == vm.EmailOrUsername || u.Username == vm.EmailOrUsername) &&
                    u.PasswordHash == vm.Password && // TODO: Hash check
                    u.Role == vm.Role &&
                    u.IsActive);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid credentials");
                return View(vm);
            }

            // TODO: اعملي Cookie / Claims / Session على حسب مشروعك
            // مؤقتًا نوجّه المستخدم على حسب ال Role:

            return user.Role switch
            {
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                "Doctor" => RedirectToAction("Dashboard", "Doctor"),
                "Receptionist" => RedirectToAction("Dashboard", "Reception"),
                "Radiologist" => RedirectToAction("Requests", "Radiology"),
                "Patient" => RedirectToAction("Index", "Home"),
                _ => RedirectToAction("Index", "Home")
            };
        }

        public IActionResult Logout()
        {
            // TODO: امسحي الـ Cookies / Session
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            // ممكن تعملي ViewModel لو حابة
            return View();
        }

        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            // TODO: ابعتي إيميل Reset لو حابة
            ViewBag.Message = "If this email exists, a reset link will be sent.";
            return View();
        }

        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            // TODO: تحققي من الـ token
            return View();
        }

        [HttpPost]
        public IActionResult ResetPassword(string token, string newPassword)
        {
            // TODO: غيّري الباسورد
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
