//using System.Diagnostics;
//using Microsoft.AspNetCore.Mvc;
//using SCMS.Models;

//namespace SCMS.Controllers
//{
//    public class HomeController : Controller
//    {
//        private readonly ILogger<HomeController> _logger;

//        public HomeController(ILogger<HomeController> logger)
//        {
//            _logger = logger;
//        }

//        public IActionResult Index()
//        {
//            return View();
//        }

//        public IActionResult Privacy()
//        {
//            return View();
//        }

//        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
//        public IActionResult Error()
//        {
//            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
//        }
//    }
//}

using Microsoft.AspNetCore.Mvc;

namespace SCMS.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Landing Page (SmartCare Clinic Management System)
            return View();
        }

        public IActionResult Feedback()
        {
            // ممكن تستدعي FeedbackController/Index أو تعمل Partial
            return RedirectToAction("Index", "Feedback");
        }

        public IActionResult Contact()
        {
            return View();
        }
    }
}

