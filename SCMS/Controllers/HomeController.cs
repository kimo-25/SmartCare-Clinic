using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SCMS.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        public IActionResult Index() => View();

        public IActionResult Feedback() => RedirectToAction("Index", "Feedback");

        public IActionResult Contact() => View();
    }
}
